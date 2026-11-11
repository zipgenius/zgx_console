#define UNICODE
#define _UNICODE
#include <windows.h>
#include <commctrl.h>
#include <shellapi.h>
#include <shlobj.h>
#include <msi.h>
#include <sddl.h>
#include <filesystem>
#include <string>
#include <vector>
#include <thread>
#include <stdexcept>
#include <fstream>
#include <algorithm>
#include "setup-config.h"

namespace fs = std::filesystem;
constexpr int UserRadio = 101, MachineRadio = 102, Status = 103, Remove = 104, Progress = 105;
constexpr UINT Completed = WM_APP + 1;
const wchar_t* UserUpgrade = L"{B69EC660-183F-42FA-9D05-0A63C6D61845}";
const wchar_t* MachineUpgrade = L"{7E0468AB-7C75-4D5E-9726-090E5D431A1A}";
bool italian = PRIMARYLANGID(GetUserDefaultUILanguage()) == LANG_ITALIAN;
bool busy = false;
std::wstring text(const wchar_t* it, const wchar_t* en) { return italian ? it : en; }
struct Failure { DWORD code; std::wstring message; };
struct Installed { std::wstring code, version; bool exists() const { return !code.empty(); } };
enum class Action { Install, Repair, Uninstall };
struct Options { bool quiet = false, machine = false, explicitScope = false; Action action = Action::Install; std::wstring planFile; } options;
Installed userInstalled, machineInstalled;
struct Result { DWORD code = 0; std::wstring error, log; Action action; };

std::wstring info(const std::wstring& product, const wchar_t* property) {
    DWORD size = 0;
    if (MsiGetProductInfoW(product.c_str(), property, nullptr, &size) != ERROR_MORE_DATA) return {};
    std::wstring value(size + 1, L'\0');
    if (MsiGetProductInfoW(product.c_str(), property, value.data(), &size) != ERROR_SUCCESS) return {};
    value.resize(size); return value;
}
std::vector<int> versionParts(const std::wstring& value) {
    std::vector<int> parts; size_t start = 0;
    do { parts.push_back(_wtoi(value.c_str() + start)); start = value.find(L'.', start); if (start != std::wstring::npos) ++start; } while (start != std::wstring::npos);
    parts.resize(4); return parts;
}
Installed detect(const wchar_t* upgrade) {
    Installed found;
    for (DWORD i = 0;; ++i) {
        wchar_t product[39]{};
        UINT status = MsiEnumRelatedProductsW(upgrade, 0, i, product);
        if (status == ERROR_NO_MORE_ITEMS) break;
        if (status != ERROR_SUCCESS) throw Failure{status, L"Cannot read Windows Installer registration."};
        if (MsiQueryProductStateW(product) <= 0) continue;
        auto version = info(product, INSTALLPROPERTY_VERSIONSTRING);
        if (!found.exists() || versionParts(version) > versionParts(found.version)) found = {product, version};
    }
    return found;
}
fs::path folder(REFKNOWNFOLDERID id) {
    PWSTR path = nullptr;
    if (FAILED(SHGetKnownFolderPath(id, 0, nullptr, &path))) throw Failure{ERROR_PATH_NOT_FOUND, L"Cannot find the Windows installation folder."};
    fs::path result(path); CoTaskMemFree(path); return result;
}
std::wstring productCode(bool machine) { return machine ? SETUP_MACHINE_PRODUCT : SETUP_USER_PRODUCT; }
Installed installed(bool machine) { return machine ? machineInstalled : userInstalled; }
fs::path workingFolder() {
    wchar_t path[32768]{};
    DWORD size = GetTempPathW(32768, path);
    if (!size || size >= 32768) throw Failure{ERROR_PATH_NOT_FOUND, L"Cannot find the Windows temporary folder."};
    return fs::path(path) / L"ZipGeniusConsoleSetup";
}
fs::path cacheFolder(bool machine, const std::wstring& code) {
    return workingFolder() / L"Packages" / (machine ? L"machine" : L"user") / code;
}
void privateDirectory(const fs::path& path) {
    // Respect the user profile's inherited ACLs, including Windows Installer
    // service/capability access; the payload contains no credentials or user data.
    fs::create_directories(path);
}
fs::path extract(bool machine) {
    auto directory = cacheFolder(machine, productCode(machine)); privateDirectory(directory);
    auto output = directory / L"package.msi";
    HRSRC resource = FindResourceW(nullptr, MAKEINTRESOURCEW(machine ? 202 : 201), RT_RCDATA);
    HGLOBAL data = resource ? LoadResource(nullptr, resource) : nullptr;
    const BYTE* bytes = data ? static_cast<const BYTE*>(LockResource(data)) : nullptr;
    DWORD size = resource ? SizeofResource(nullptr, resource) : 0;
    if (!bytes || !size) throw Failure{ERROR_RESOURCE_DATA_NOT_FOUND, L"The installer payload is missing."};
    auto temporary = directory / L"package.new";
    HANDLE file = CreateFileW(temporary.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE) throw Failure{GetLastError(), L"Cannot prepare the installer payload."};
    DWORD error = 0;
    for (DWORD offset = 0; offset < size;) {
        DWORD count = (std::min)(size - offset, DWORD(4 * 1024 * 1024)), written = 0;
        if (!WriteFile(file, bytes + offset, count, &written, nullptr) || written != count) { error = GetLastError(); if (!error) error = ERROR_WRITE_FAULT; break; }
        offset += written;
    }
    CloseHandle(file);
    if (!error && !MoveFileExW(temporary.c_str(), output.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)) error = GetLastError();
    if (error) { DeleteFileW(temporary.c_str()); throw Failure{error, L"Cannot prepare the installer payload."}; }
    return output;
}
void removeCache(bool machine, const std::wstring& code) {
    if (code.empty()) return;
    auto directory = cacheFolder(machine, code);
    DeleteFileW((directory / L"package.msi").c_str());
    RemoveDirectoryW(directory.c_str()); // Only remove an empty, product-specific cache directory.
}
std::wstring quote(const std::wstring& value) { return L"\"" + value + L"\""; }
fs::path makeLog() {
    auto directory = workingFolder() / L"Logs";
    fs::create_directories(directory);
    SYSTEMTIME now{}; GetLocalTime(&now); wchar_t name[100]{};
    swprintf_s(name, L"setup-%04u%02u%02u-%02u%02u%02u-%lu.log", now.wYear, now.wMonth, now.wDay, now.wHour, now.wMinute, now.wSecond, GetCurrentProcessId());
    return directory / name;
}
void validate(const Options& selected) {
    auto current = installed(selected.machine), other = installed(!selected.machine);
    if (!current.exists() && other.exists()) throw Failure{ERROR_PRODUCT_VERSION, text(L"È già presente un'installazione nell'altro ambito. Rimuovila prima di cambiare modalità.", L"An installation already exists in the other scope. Remove it before changing scope.")};
    if (selected.action == Action::Uninstall && !current.exists()) throw Failure{ERROR_UNKNOWN_PRODUCT, text(L"Nessuna installazione da rimuovere.", L"There is no installation to remove.")};
    if (selected.action != Action::Uninstall && current.exists() && versionParts(current.version) > versionParts(SETUP_VERSION)) throw Failure{ERROR_PRODUCT_VERSION, text(L"È già installata una versione più recente. Usa il suo programma di installazione.", L"A newer version is already installed. Use its setup program.")};
    if (selected.action == Action::Repair && (!current.exists() || _wcsicmp(current.code.c_str(), productCode(selected.machine).c_str()))) throw Failure{ERROR_PRODUCT_VERSION, text(L"Per questa versione usa Aggiorna, oppure il programma di installazione originale per ripararla.", L"Update this version, or use its original setup program to repair it.")};
}
Result perform(Options selected, HWND owner) {
    Result result; result.action = selected.action;
    try {
        validate(selected);
        auto current = installed(selected.machine);
        bool repair = selected.action == Action::Repair || (selected.action == Action::Install && current.exists() && !_wcsicmp(current.code.c_str(), productCode(selected.machine).c_str()));
        std::wstring parameters;
        if (selected.action == Action::Uninstall) parameters = L"/x " + current.code;
        else {
            parameters = L"/i " + quote(extract(selected.machine).wstring());
            if (repair) parameters += L" REINSTALL=ALL REINSTALLMODE=vomus";
        }
        auto log = makeLog(); result.log = log.wstring();
        parameters += selected.quiet ? L" /qn" : L" /passive";
        parameters += L" /norestart /l*v " + quote(log.wstring());
        wchar_t system[MAX_PATH]{}; GetSystemDirectoryW(system, MAX_PATH);
        auto executable = fs::path(system) / L"msiexec.exe";
        HANDLE process = nullptr;
        if (selected.machine) {
            SHELLEXECUTEINFOW start{sizeof(start)};
            start.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NOASYNC;
            start.hwnd = owner; start.lpVerb = L"runas";
            start.lpFile = executable.c_str(); start.lpParameters = parameters.c_str(); start.nShow = SW_HIDE;
            if (!ShellExecuteExW(&start)) throw Failure{GetLastError(), text(L"Installazione non avviata o autorizzazione annullata.", L"Setup did not start or authorization was cancelled.")};
            process = start.hProcess;
        } else {
            // Keep the exact caller token; never delegate per-user launch to a shell broker.
            STARTUPINFOW start{sizeof(start)}; PROCESS_INFORMATION child{};
            auto command = quote(executable.wstring()) + L" " + parameters;
            if (!CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &start, &child))
                throw Failure{GetLastError(), L"Cannot start Windows Installer for the current user."};
            CloseHandle(child.hThread); process = child.hProcess;
        }
        WaitForSingleObject(process, INFINITE);
        GetExitCodeProcess(process, &result.code); CloseHandle(process);
        if (result.code != ERROR_SUCCESS && result.code != ERROR_SUCCESS_REBOOT_REQUIRED) throw Failure{result.code, text(L"Windows Installer non ha completato l'operazione.", L"Windows Installer did not complete the operation.")};
        if (selected.action == Action::Uninstall) removeCache(selected.machine, current.code);
        else if (current.exists() && _wcsicmp(current.code.c_str(), productCode(selected.machine).c_str()) && MsiQueryProductStateW(current.code.c_str()) <= 0) removeCache(selected.machine, current.code);
    } catch (const Failure& error) { result.code = error.code; result.error = error.message; }
    catch (const std::exception&) { result.code = ERROR_INSTALL_FAILURE; result.error = text(L"Impossibile preparare l'installazione. Controlla lo spazio e i permessi della cartella personale.", L"Cannot prepare setup. Check free space and permissions in your user folder."); }
    if (!result.error.empty() && !result.log.empty()) {
        std::wofstream log(fs::path(result.log + L".setup.txt")); log << result.error << L"\nCode: " << result.code;
    }
    return result;
}
void refresh(HWND window) {
    options.machine = IsDlgButtonChecked(window, MachineRadio) == BST_CHECKED;
    auto current = installed(options.machine);
    bool repair = current.exists() && !_wcsicmp(current.code.c_str(), productCode(options.machine).c_str());
    SetDlgItemTextW(window, IDOK, current.exists() ? (repair ? text(L"Ripara", L"Repair").c_str() : text(L"Aggiorna", L"Update").c_str()) : text(L"Installa", L"Install").c_str());
    EnableWindow(GetDlgItem(window, Remove), current.exists());
    SendDlgItemMessageW(window, IDOK, BCM_SETSHIELD, 0, options.machine);
    SendDlgItemMessageW(window, Remove, BCM_SETSHIELD, 0, options.machine);
    auto message = text(L"Scegli per chi installare la console. La protezione ZipBomb è sempre attiva.", L"Choose who can use the console. ZipBomb protection is always active.");
    if (current.exists()) message = text(L"Versione installata: ", L"Installed version: ") + current.version + text(L". L'ambito dell'installazione viene mantenuto.", L". The installation scope is preserved.");
    SetDlgItemTextW(window, Status, message.c_str());
}
INT_PTR CALLBACK dialog(HWND window, UINT message, WPARAM wParam, LPARAM lParam) {
    if (message == WM_INITDIALOG) {
        ShowWindow(GetDlgItem(window, Progress), SW_HIDE);
        SetDlgItemTextW(window, 106, (L"ZipGenius X Console — " + std::wstring(SETUP_VERSION)).c_str());
        SetDlgItemTextW(window, UserRadio, text(L"Solo per me (scelta consigliata)", L"Only for me (recommended)").c_str());
        SetDlgItemTextW(window, MachineRadio, text(L"Per tutti gli utenti del computer", L"For everyone using this computer").c_str());
        SetDlgItemTextW(window, 107, text(L"Cartella personale — nessun privilegio amministrativo", L"User folder — no administrator privileges").c_str());
        SetDlgItemTextW(window, 108, text(L"Program Files — richiede autorizzazione amministrativa", L"Program Files — requires administrator authorization").c_str());
        SetDlgItemTextW(window, Remove, text(L"Disinstalla", L"Uninstall").c_str());
        SetDlgItemTextW(window, IDCANCEL, text(L"Annulla", L"Cancel").c_str());
        CheckRadioButton(window, UserRadio, MachineRadio, options.machine ? MachineRadio : UserRadio);
        if (userInstalled.exists() != machineInstalled.exists()) {
            EnableWindow(GetDlgItem(window, UserRadio), FALSE); EnableWindow(GetDlgItem(window, MachineRadio), FALSE);
        }
        refresh(window); return TRUE;
    }
    if (message == WM_COMMAND) {
        int id = LOWORD(wParam);
        if (id == IDCANCEL) { if (!busy) EndDialog(window, ERROR_INSTALL_USEREXIT); return TRUE; }
        if (id == UserRadio || id == MachineRadio) { refresh(window); return TRUE; }
        if ((id == IDOK || id == Remove) && !busy) {
            Options selected = options;
            selected.action = id == Remove ? Action::Uninstall : Action::Install;
            busy = true;
            for (int control : {UserRadio, MachineRadio, IDOK, Remove, IDCANCEL}) EnableWindow(GetDlgItem(window, control), FALSE);
            SetDlgItemTextW(window, Status, text(L"Operazione in corso. Attendi il completamento di Windows Installer…", L"Working. Please wait for Windows Installer to finish…").c_str());
            ShowWindow(GetDlgItem(window, Progress), SW_SHOW);
            SendDlgItemMessageW(window, Progress, PBM_SETMARQUEE, TRUE, 30);
            std::thread([window, selected] { auto* result = new Result(perform(selected, window)); PostMessageW(window, Completed, 0, reinterpret_cast<LPARAM>(result)); }).detach();
            return TRUE;
        }
    }
    if (message == Completed) {
        auto* result = reinterpret_cast<Result*>(lParam); busy = false;
        if (result->code == 0 || result->code == ERROR_SUCCESS_REBOOT_REQUIRED) {
            auto success = result->action == Action::Uninstall ? text(L"Rimozione completata.", L"Uninstallation complete.") : text(L"Installazione completata. Chiudi e riapri completamente il terminale per usare zg.", L"Setup complete. Fully close and reopen your terminal to use zg.");
            if (result->code == ERROR_SUCCESS_REBOOT_REQUIRED) success += text(L" È necessario riavviare Windows.", L" Windows must be restarted.");
            MessageBoxW(window, success.c_str(), L"ZipGenius X Console", MB_OK | MB_ICONINFORMATION);
            DWORD code = result->code; delete result; EndDialog(window, code);
        } else {
            auto error = result->error + L"\n\n" + text(L"Codice: ", L"Code: ") + std::to_wstring(result->code);
            if (!result->log.empty()) error += L"\nLog: " + result->log;
            MessageBoxW(window, error.c_str(), L"ZipGenius X Console", MB_OK | MB_ICONERROR); delete result;
            ShowWindow(GetDlgItem(window, Progress), SW_HIDE);
            EnableWindow(GetDlgItem(window, IDOK), TRUE); EnableWindow(GetDlgItem(window, IDCANCEL), TRUE);
            if (userInstalled.exists() == machineInstalled.exists()) { EnableWindow(GetDlgItem(window, UserRadio), TRUE); EnableWindow(GetDlgItem(window, MachineRadio), TRUE); }
            refresh(window);
        }
        return TRUE;
    }
    if (message == WM_CLOSE) { if (!busy) EndDialog(window, ERROR_INSTALL_USEREXIT); return TRUE; }
    return FALSE;
}
std::wstring json(std::wstring value) {
    std::wstring escaped;
    for (wchar_t c : value) { if (c == L'\\' || c == L'"') escaped += L'\\'; escaped += c; }
    return L"\"" + escaped + L"\"";
}
std::string utf8(const std::wstring& value) {
    if (value.empty()) return {};
    int count = WideCharToMultiByte(CP_UTF8, 0, value.data(), static_cast<int>(value.size()), nullptr, 0, nullptr, nullptr);
    std::string output(count, '\0');
    WideCharToMultiByte(CP_UTF8, 0, value.data(), static_cast<int>(value.size()), output.data(), count, nullptr, nullptr);
    return output;
}
int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int) {
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    INITCOMMONCONTROLSEX controls{sizeof(controls), ICC_PROGRESS_CLASS}; InitCommonControlsEx(&controls);
    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    HANDLE mutex = CreateMutexW(nullptr, TRUE, L"Local\\ZipGeniusConsoleUnifiedSetup");
    if (!mutex || GetLastError() == ERROR_ALREADY_EXISTS) { if (mutex) CloseHandle(mutex); return ERROR_INSTALL_ALREADY_RUNNING; }
    DWORD exit = 0;
    try {
        int count = 0; auto arguments = CommandLineToArgvW(GetCommandLineW(), &count);
        for (int i = 1; i < count; ++i) {
            std::wstring arg = arguments[i];
            if (!_wcsicmp(arg.c_str(), L"/quiet")) options.quiet = true;
            else if (!_wcsicmp(arg.c_str(), L"/allusers")) { options.machine = true; options.explicitScope = true; }
            else if (!_wcsicmp(arg.c_str(), L"/currentuser")) { options.machine = false; options.explicitScope = true; }
            else if (!_wcsicmp(arg.c_str(), L"/repair")) options.action = Action::Repair;
            else if (!_wcsicmp(arg.c_str(), L"/uninstall")) options.action = Action::Uninstall;
            else if (!_wcsicmp(arg.c_str(), L"/plan") && i + 1 < count) options.planFile = arguments[++i];
            else throw Failure{ERROR_INVALID_PARAMETER, L"Usage: setup.exe [/quiet] [/currentuser|/allusers] [/repair|/uninstall]"};
        }
        LocalFree(arguments);
        userInstalled = detect(UserUpgrade); machineInstalled = detect(MachineUpgrade);
        if (!options.explicitScope && (userInstalled.exists() != machineInstalled.exists())) options.machine = machineInstalled.exists();
        validate(options);
        if (!options.planFile.empty()) {
            auto destination = options.machine ? folder(FOLDERID_ProgramFilesX64) / L"ZipGenius X Console" : folder(FOLDERID_LocalAppData) / L"Programs" / L"ZipGenius X Console";
            std::ofstream output{fs::path(options.planFile)}; if (!output) throw Failure{ERROR_WRITE_FAULT, L"Cannot write the setup plan."};
            output << utf8(L"{\"scope\":" + json(options.machine ? L"machine" : L"user") + L",\"elevationRequired\":" + (options.machine ? L"true" : L"false") + L",\"installed\":" + (installed(options.machine).exists() ? L"true" : L"false") + L",\"productCode\":" + json(installed(options.machine).code) + L",\"target\":" + json(destination.wstring()) + L"}");
        } else if (options.quiet) exit = perform(options, nullptr).code;
        else if (options.action != Action::Install) { auto result = perform(options, nullptr); exit = result.code; if (!result.error.empty()) MessageBoxW(nullptr, result.error.c_str(), L"ZipGenius X Console", MB_OK | MB_ICONERROR); }
        else exit = static_cast<DWORD>(DialogBoxParamW(instance, MAKEINTRESOURCEW(100), nullptr, dialog, 0));
    } catch (const Failure& failure) { exit = failure.code; if (!options.quiet) MessageBoxW(nullptr, failure.message.c_str(), L"ZipGenius X Console", MB_OK | MB_ICONERROR); }
    catch (const std::exception&) { exit = ERROR_INSTALL_FAILURE; }
    ReleaseMutex(mutex); CloseHandle(mutex); CoUninitialize(); return static_cast<int>(exit);
}
