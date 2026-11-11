#include "sfx_format.h"
#include "archive_paths.h"
#include "password_errors.h"
#include "resource.h"
#include <algorithm>
#include <filesystem>
#include <fstream>
#include <memory>
#include <string>
#include <thread>
#include <vector>
#include <shlobj.h>
#include <shobjidl.h>
#include <shellapi.h>
#include <commctrl.h>
#include <uxtheme.h>
#include <dwmapi.h>
#include <gdiplus.h>

#pragma comment(linker, "/manifestdependency:\"type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")

namespace fs = std::filesystem;
static HINSTANCE instance;
static HWND windowHandle, destinationBox, filesBox, filesToggle, extractButton, readmeButton, progressBar, progressLabel, browseButton, copyrightLink;
static HWND messageControl, iconControl;
static sfx::Footer footer{};
static fs::path work, archiveFile, engineFile;
static std::wstring password;
static bool encryptedEntries = false;
static bool passwordProvided = false;
static UINT listingError = 0;
static std::vector<std::wstring> entries;
static std::wstring readmePath;
static bool extracting = false;
static bool fixedDestination = false;
static bool filesExpanded = false;
static fs::path resolvedDestination;
static constexpr UINT extractionFinished = WM_APP + 1;
static constexpr UINT extractionProgress = WM_APP + 2;
static constexpr UINT requestNextPassword = WM_APP + 3;
struct ExtractionResult { bool success; std::wstring error; };
static UINT uiDpi = 96;
static int messageExtra = 0;
static HFONT bodyFont, headingFont, labelFont, copyrightFont;
static HBRUSH whiteBrush, sideBrush;
static HICON largeIcon;
static HBITMAP sidebarBitmap;
static ULONG_PTR gdiplusToken;
static std::wstring text(UINT id) {
    const wchar_t* value = nullptr;
    const int length = LoadStringW(instance, id, reinterpret_cast<wchar_t*>(&value), 0);
    return length > 0 ? std::wstring(value, length) : std::wstring();
}
struct SfxError { UINT id; };
static std::wstring system_error_text(const char* value) {
    const int length = MultiByteToWideChar(CP_ACP, 0, value, -1, nullptr, 0);
    if (length <= 1) return {};
    std::wstring result(length, L'\0');
    MultiByteToWideChar(CP_ACP, 0, value, -1, result.data(), length);
    result.pop_back();
    return result;
}
static int scale(int value) { return MulDiv(value, uiDpi, 96); }
static HBITMAP render_sidebar_icon() {
    HICON sourceIcon = static_cast<HICON>(LoadImageW(instance, MAKEINTRESOURCEW(1), IMAGE_ICON, 256, 256, LR_DEFAULTCOLOR));
    if (!sourceIcon) return nullptr;
    std::unique_ptr<Gdiplus::Bitmap> source(Gdiplus::Bitmap::FromHICON(sourceIcon));
    HBITMAP result = nullptr;
    if (source && source->GetLastStatus() == Gdiplus::Ok) {
        const int size = scale(64);
        Gdiplus::Bitmap target(size, size, PixelFormat32bppARGB);
        Gdiplus::Graphics graphics(&target);
        graphics.Clear(Gdiplus::Color(255, 245, 247, 250));
        graphics.SetInterpolationMode(Gdiplus::InterpolationModeHighQualityBicubic);
        graphics.SetPixelOffsetMode(Gdiplus::PixelOffsetModeHighQuality);
        if (graphics.DrawImage(source.get(), Gdiplus::Rect(0, 0, size, size),
                0, 0, source->GetWidth(), source->GetHeight(), Gdiplus::UnitPixel) == Gdiplus::Ok)
            target.GetHBITMAP(Gdiplus::Color(245, 247, 250), &result);
    }
    DestroyIcon(sourceIcon);
    return result;
}
static HFONT make_font(int points, int weight) {
    return CreateFontW(-MulDiv(points, uiDpi, 72), 0, 0, 0, weight, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
}
static void apply_font(HWND control, HFONT font = nullptr) {
    SendMessageW(control, WM_SETFONT, reinterpret_cast<WPARAM>(font ? font : bodyFont), TRUE);
}

static std::wstring utf8(const std::string& input) {
    int n = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, input.c_str(), static_cast<int>(input.size()), nullptr, 0);
    if (!n) return {};
    std::wstring out(n, L'\0');
    MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, input.c_str(), static_cast<int>(input.size()), out.data(), n);
    return out;
}
static std::wstring quote_arg(const std::wstring& value) {
    std::wstring out = L"\"";
    size_t slashes = 0;
    for (wchar_t c : value) {
        if (c == L'\\') { ++slashes; continue; }
        out.append(slashes * (c == L'"' ? 2 : 1), L'\\');
        slashes = 0;
        if (c == L'"') out += L'\\';
        out += c;
    }
    out.append(slashes * 2, L'\\');
    out += L'"';
    return out;
}
static std::wstring edit_text(HWND h) {
    int n = GetWindowTextLengthW(h);
    std::wstring value(n + 1, L'\0');
    GetWindowTextW(h, value.data(), n + 1);
    value.resize(n);
    return value;
}
static bool write_resource(int id, const fs::path& target) {
    auto resource = FindResourceW(instance, MAKEINTRESOURCEW(id), RT_RCDATA);
    if (!resource) return false;
    auto data = LoadResource(instance, resource);
    auto bytes = static_cast<const char*>(LockResource(data));
    std::ofstream out(target, std::ios::binary);
    out.write(bytes, SizeofResource(instance, resource));
    return bool(out);
}
static bool load_payload(const fs::path& self) {
    std::ifstream in(self, std::ios::binary | std::ios::ate);
    if (!in || in.tellg() < static_cast<std::streamoff>(sizeof footer)) return false;
    uint64_t total = static_cast<uint64_t>(in.tellg());
    in.seekg(total - sizeof footer);
    in.read(reinterpret_cast<char*>(&footer), sizeof footer);
    if (!in || !std::equal(std::begin(sfx::magic), std::end(sfx::magic), footer.signature)
        || footer.schema != sfx::version || footer.archiveLength == 0
        || footer.archiveOffset > total - sizeof footer || footer.archiveLength != total - footer.archiveOffset - sizeof footer
        || !std::wmemchr(footer.runPath, L'\0', 260)
        || !std::wmemchr(footer.message, L'\0', 320)
        || !std::wmemchr(footer.destinationSubpath, L'\0', 128)
        || !std::wmemchr(footer.archiveName, L'\0', 260)
        || !footer.archiveName[0] || wcspbrk(footer.archiveName, L"/\\")
        || footer.destinationFolder > sfx::Favorites
        || (footer.destinationFolder == sfx::Editable && footer.destinationSubpath[0])
        || (footer.destinationSubpath[0] && !sfx::valid_relative(footer.destinationSubpath))
        || (footer.format != sfx::Zip && footer.format != sfx::SevenZip && footer.format != sfx::Rar)) return false;
    wchar_t tmp[MAX_PATH];
    if (!GetTempPathW(MAX_PATH, tmp)) return false;
    wchar_t guid[64];
    wsprintfW(guid, L"ZipGeniusSfx-%lu-%lu", GetCurrentProcessId(), GetTickCount());
    work = fs::path(tmp) / guid;
    fs::create_directories(work);
    engineFile = work / L"7z.exe";
    if (!write_resource(101, engineFile) || !write_resource(102, work / L"7z.dll")) return false;
    archiveFile = work / (footer.format == sfx::Zip ? L"payload.zip" : footer.format == sfx::SevenZip ? L"payload.7z" : L"payload.rar");
    std::ofstream out(archiveFile, std::ios::binary);
    in.seekg(footer.archiveOffset);
    std::vector<char> buffer(1 << 20);
    uint64_t remaining = footer.archiveLength;
    while (remaining) {
        size_t size = static_cast<size_t>(std::min<uint64_t>(remaining, buffer.size()));
        in.read(buffer.data(), size);
        out.write(buffer.data(), size);
        if (!in || !out) return false;
        remaining -= size;
    }
    return true;
}
static bool resolve_destination() {
    if (footer.destinationFolder == sfx::Editable) return true;
    const KNOWNFOLDERID* folder = nullptr;
    switch (footer.destinationFolder) {
    case sfx::Desktop: folder = &FOLDERID_Desktop; break;
    case sfx::Documents: folder = &FOLDERID_Documents; break;
    case sfx::Downloads: folder = &FOLDERID_Downloads; break;
    case sfx::Pictures: folder = &FOLDERID_Pictures; break;
    case sfx::Music: folder = &FOLDERID_Music; break;
    case sfx::Videos: folder = &FOLDERID_Videos; break;
    case sfx::Favorites: folder = &FOLDERID_Favorites; break;
    default: return false;
    }
    PWSTR systemPath = nullptr;
    if (FAILED(SHGetKnownFolderPath(*folder, 0, nullptr, &systemPath))) return false;
    resolvedDestination = systemPath;
    CoTaskMemFree(systemPath);
    if (footer.destinationSubpath[0]) resolvedDestination /= footer.destinationSubpath;
    fixedDestination = true;
    return true;
}
static DWORD run_engine(const std::wstring& args, std::string* captured = nullptr, bool reportProgress = false) {
    SECURITY_ATTRIBUTES sa{sizeof sa, nullptr, TRUE};
    HANDLE readPipe = nullptr, writePipe = nullptr;
    if (!CreatePipe(&readPipe, &writePipe, &sa, 0)) return DWORD(-1);
    SetHandleInformation(readPipe, HANDLE_FLAG_INHERIT, 0);
    STARTUPINFOW si{sizeof si};
    si.dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;
    si.hStdOutput = writePipe;
    si.hStdError = writePipe;
    si.hStdInput = GetStdHandle(STD_INPUT_HANDLE);
    PROCESS_INFORMATION pi{};
    std::wstring command = quote_arg(engineFile.wstring()) + L" " + args +
        (reportProgress ? L" -sccUTF-8 -bsp1" : L" -sccUTF-8 -bd");
    std::vector<wchar_t> mutableCommand(command.begin(), command.end());
    mutableCommand.push_back(0);
    BOOL started = CreateProcessW(engineFile.c_str(), mutableCommand.data(), nullptr, nullptr, TRUE,
        CREATE_NO_WINDOW, nullptr, work.c_str(), &si, &pi);
    CloseHandle(writePipe);
    if (!started) { CloseHandle(readPipe); return DWORD(-1); }
    char chunk[8192]; DWORD count;
    int digits = 0, percentage = 0, lastReported = -1;
    while (ReadFile(readPipe, chunk, sizeof chunk, &count, nullptr) && count) {
        if (captured && captured->size() < 16 * 1024 * 1024) captured->append(chunk, count);
        if (reportProgress) for (DWORD i = 0; i < count; ++i) {
            const char c = chunk[i];
            if (c >= '0' && c <= '9') {
                percentage = std::min(1000, percentage * 10 + c - '0');
                ++digits;
            } else {
                if (c == '%' && digits > 0 && digits <= 3 && percentage <= 100) {
                    const int position = percentage * 90 / 100;
                    if (position > lastReported) {
                        PostMessageW(windowHandle, extractionProgress, position, 0);
                        lastReported = position;
                    }
                }
                digits = percentage = 0;
            }
        }
    }
    CloseHandle(readPipe);
    WaitForSingleObject(pi.hProcess, INFINITE);
    DWORD result = DWORD(-1);
    GetExitCodeProcess(pi.hProcess, &result);
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return result;
}
static std::wstring password_arg() { return password.empty() ? L" -p\"\"" : L" -p" + quote_arg(password); }
static bool list_entries() {
    listingError = 0;
    std::string output;
    DWORD code = run_engine(L"l -slt " + quote_arg(archiveFile.wstring()) + password_arg(), &output);
    if (code != 0) return false;
    entries.clear(); readmePath.clear();
    encryptedEntries = false;
    sfx::ArchivePaths paths;
    size_t separator = output.find("----------");
    if (separator == std::string::npos) return false;
    size_t position = output.find('\n', separator);
    while (position != std::string::npos && position + 1 < output.size()) {
        size_t end = output.find('\n', position + 1);
        std::string line = output.substr(position + 1, end == std::string::npos ? end : end - position - 1);
        if (!line.empty() && line.back() == '\r') line.pop_back();
        if (line == "Encrypted = +") encryptedEntries = true;
        if (line.rfind("Path = ", 0) == 0) {
            auto name = utf8(line.substr(7));
            while (!name.empty() && (name.back() == L'/' || name.back() == L'\\')) name.pop_back();
            if (!paths.add(name)) { listingError = IDS_UNSAFE_PATH; return false; }
            entries.push_back(name);
            if (readmePath.empty() && _wcsicmp(fs::path(name).filename().c_str(), L"readme.txt") == 0) readmePath = name;
        }
        position = end;
    }
    return !entries.empty();
}
static INT_PTR CALLBACK password_dialog(HWND dialog, UINT message, WPARAM wp, LPARAM) {
    if (message == WM_INITDIALOG) {
        RECT owner{}, bounds{};
        GetWindowRect(windowHandle, &owner);
        GetWindowRect(dialog, &bounds);
        const int width = bounds.right - bounds.left, height = bounds.bottom - bounds.top;
        MONITORINFO monitor{sizeof monitor};
        GetMonitorInfoW(MonitorFromWindow(windowHandle, MONITOR_DEFAULTTONEAREST), &monitor);
        const int x = std::clamp(owner.left + (owner.right - owner.left - width) / 2,
            monitor.rcWork.left, std::max(monitor.rcWork.left, monitor.rcWork.right - width));
        const int y = std::clamp(owner.top + (owner.bottom - owner.top - height) / 2,
            monitor.rcWork.top, std::max(monitor.rcWork.top, monitor.rcWork.bottom - height));
        SetWindowPos(dialog, nullptr, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
        SendDlgItemMessageW(dialog, IDC_PASSWORD_INPUT, EM_SETLIMITTEXT, 1024, 0);
        SetFocus(GetDlgItem(dialog, IDC_PASSWORD_INPUT));
        return FALSE;
    }
    if (message == WM_COMMAND) {
        if (LOWORD(wp) == IDOK) {
            password = edit_text(GetDlgItem(dialog, IDC_PASSWORD_INPUT));
            EndDialog(dialog, IDOK);
            return TRUE;
        }
        if (LOWORD(wp) == IDCANCEL) {
            EndDialog(dialog, IDCANCEL);
            return TRUE;
        }
    }
    if (message == WM_CLOSE) {
        EndDialog(dialog, IDCANCEL);
        return TRUE;
    }
    return FALSE;
}
static bool ask_password() {
    const bool accepted = DialogBoxParamW(instance, MAKEINTRESOURCEW(IDD_PASSWORD), windowHandle,
        password_dialog, 0) == IDOK;
    if (accepted) passwordProvided = true;
    return accepted;
}
static bool ensure_list(bool requirePassword = false) {
    for (int attempt = 0; attempt <= 3; ++attempt) {
        const bool listed = list_entries();
        if (listed && (!requirePassword || !encryptedEntries || passwordProvided)) return true;
        if (listingError) {
            MessageBoxW(windowHandle, text(listingError).c_str(), L"ZipGenius SFX", MB_OK | MB_ICONERROR);
            return false;
        }
        if (attempt == 3) return false;
        // ZIP usually exposes its directory without a password. Listing success
        // therefore does not mean the encrypted file contents can be extracted.
        if (!listed && MessageBoxW(windowHandle, text(IDS_PASSWORD_PROMPT).c_str(), L"ZipGenius SFX", MB_OKCANCEL | MB_ICONWARNING) != IDOK) return false;
        if (!ask_password()) return false;
    }
    return false;
}
static bool safe_tree(const fs::path& root) {
    for (const auto& entry : fs::recursive_directory_iterator(root)) {
        if (entry.is_symlink()) return false;
        if (!entry.is_regular_file() && !entry.is_directory()) return false;
    }
    return true;
}
struct CopyProgress { uint64_t total; uint64_t completed; int lastReported; };
static DWORD CALLBACK copy_progress(LARGE_INTEGER, LARGE_INTEGER transferred, LARGE_INTEGER, LARGE_INTEGER,
    DWORD, DWORD, HANDLE, HANDLE, LPVOID context) {
    auto& progress = *static_cast<CopyProgress*>(context);
    const long double fraction = static_cast<long double>(progress.completed + transferred.QuadPart) / progress.total;
    const int position = 90 + static_cast<int>(std::min(1.0L, fraction) * 10);
    if (position > progress.lastReported) {
        PostMessageW(windowHandle, extractionProgress, position, 0);
        progress.lastReported = position;
    }
    return PROGRESS_CONTINUE;
}
static void copy_to_destination(const fs::path& stage, const fs::path& destination) {
    uint64_t total = 0;
    for (const auto& entry : fs::recursive_directory_iterator(stage))
        if (entry.is_regular_file()) total += entry.file_size();
    CopyProgress progress{total, 0, 89};
    fs::create_directories(destination);
    for (const auto& entry : fs::recursive_directory_iterator(stage)) {
        const fs::path target = destination / entry.path().lexically_relative(stage);
        if (entry.is_directory()) {
            fs::create_directories(target);
        } else {
            fs::create_directories(target.parent_path());
            if (!CopyFileExW(entry.path().c_str(), target.c_str(),
                    total ? copy_progress : nullptr, total ? &progress : nullptr, nullptr, 0))
                throw SfxError{IDS_COPY_FAILED};
            progress.completed += entry.file_size();
        }
    }
    PostMessageW(windowHandle, extractionProgress, 100, 0);
}
static void extract_worker(fs::path destination) {
    auto* outcome = new ExtractionResult{false, {}};
    try {
        if (destination.empty()) throw SfxError{IDS_EMPTY_DESTINATION};
        fs::path stage = work / L"stage";
        fs::create_directories(stage);
        std::wstring selection;
        for (;;) {
            std::string output;
            DWORD result = run_engine(L"x -y -spd " + quote_arg(archiveFile.wstring()) +
                L" -o" + quote_arg(stage.wstring()) + password_arg() + selection, &output, true);
            if (result == 0) break;
            std::vector<std::string> failed;
            if (!sfx::password_failures(output, failed)) throw SfxError{IDS_EXTRACTION_FAILED};
            const fs::path retryList = work / L"retry-files.txt";
            std::ofstream list(retryList, std::ios::binary | std::ios::trunc);
            for (const auto& name : failed) {
                const auto wideName = utf8(name);
                // Do not allow engine diagnostics to introduce unlisted paths
                // or list-file directives. Names have already passed safety checks.
                if (std::find(entries.begin(), entries.end(), wideName) == entries.end())
                    throw SfxError{IDS_EXTRACTION_FAILED};
                list << name << '\n';
            }
            list.close();
            if (!list) throw SfxError{IDS_EXTRACTION_FAILED};
            // A synchronous UI message keeps all dialog operations on the UI
            // thread; this worker waits without touching the password meanwhile.
            if (!SendMessageW(windowHandle, requestNextPassword, failed.size(), 0)) {
                if (!PostMessageW(windowHandle, extractionFinished, 0, reinterpret_cast<LPARAM>(outcome))) delete outcome;
                return;
            }
            selection = L" -scsUTF-8 -i@" + quote_arg(retryList.wstring());
        }
        PostMessageW(windowHandle, extractionProgress, 90, 0);
        if (!safe_tree(stage)) throw SfxError{IDS_UNSAFE_LINKS};
        if ((footer.flags & sfx::RunAfter) && footer.runPath[0] && !fs::is_regular_file(stage / footer.runPath))
            throw SfxError{IDS_RUN_MISSING};
        copy_to_destination(stage, destination);
        if ((footer.flags & sfx::RunAfter) && footer.runPath[0]) {
            fs::path relative(footer.runPath);
            if (!sfx::valid_relative(relative.wstring())) throw SfxError{IDS_RUN_INVALID};
            fs::path target = destination / relative;
            if (!fs::is_regular_file(target)) throw SfxError{IDS_RUN_NOT_FOUND};
            if (reinterpret_cast<INT_PTR>(ShellExecuteW(windowHandle, L"open", target.c_str(), nullptr, destination.c_str(), SW_SHOWNORMAL)) <= 32)
                throw SfxError{IDS_RUN_FAILED};
        }
        outcome->success = true;
    } catch (const SfxError& ex) {
        outcome->error = text(ex.id);
    } catch (const std::exception& ex) {
        outcome->error = system_error_text(ex.what());
    }
    if (!PostMessageW(windowHandle, extractionFinished, 0, reinterpret_cast<LPARAM>(outcome))) delete outcome;
}
static void extract() {
    if (extracting) return;
    extracting = true;
    EnableWindow(extractButton, FALSE);
    EnableWindow(destinationBox, FALSE);
    EnableWindow(browseButton, FALSE);
    if (readmeButton) EnableWindow(readmeButton, FALSE);
    if (!ensure_list(true)) {
        extracting = false;
        EnableWindow(extractButton, TRUE);
        EnableWindow(destinationBox, TRUE);
        EnableWindow(browseButton, TRUE);
        if (readmeButton) EnableWindow(readmeButton, TRUE);
        return;
    }
    ShowWindow(progressBar, SW_SHOW);
    ShowWindow(progressLabel, SW_SHOW);
    SendMessageW(progressBar, PBM_SETPOS, 0, 0);
    SetWindowTextW(progressLabel, L"0%");
    std::thread(extract_worker, fixedDestination ? resolvedDestination : fs::path(edit_text(destinationBox))).detach();
}
static void show_readme() {
    if (!ensure_list(true) || readmePath.empty()) return;
    fs::path destination = work / L"readme";
    fs::create_directories(destination);
    for (;;) {
        std::string output;
        if (run_engine(L"e -y -spd " + quote_arg(archiveFile.wstring()) + L" " + quote_arg(readmePath) + L" -o" + quote_arg(destination.wstring()) + password_arg(), &output) == 0) {
            ShellExecuteW(windowHandle, L"open", (destination / fs::path(readmePath).filename()).c_str(), nullptr, nullptr, SW_SHOWNORMAL);
            break;
        }
        std::vector<std::string> failed;
        if (!sfx::password_failures(output, failed)) {
            MessageBoxW(windowHandle, text(IDS_EXTRACTION_FAILED).c_str(), L"ZipGenius SFX", MB_OK | MB_ICONERROR);
            break;
        }
        MessageBoxW(windowHandle, text(IDS_MORE_PASSWORDS).c_str(), L"ZipGenius SFX", MB_OK | MB_ICONINFORMATION);
        if (!ask_password()) break;
    }
}
static void choose_destination(HWND owner) {
    IFileOpenDialog* picker = nullptr;
    if (FAILED(CoCreateInstance(CLSID_FileOpenDialog, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&picker)))) return;
    DWORD options = 0;
    if (SUCCEEDED(picker->GetOptions(&options)))
        picker->SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM);
    picker->SetTitle(text(IDS_CHOOSE_DESTINATION).c_str());
    const auto current = edit_text(destinationBox);
    IShellItem* initial = nullptr;
    if (!current.empty() && SUCCEEDED(SHCreateItemFromParsingName(current.c_str(), nullptr, IID_PPV_ARGS(&initial)))) {
        picker->SetFolder(initial);
        initial->Release();
    }
    if (SUCCEEDED(picker->Show(owner))) {
        IShellItem* selected = nullptr;
        if (SUCCEEDED(picker->GetResult(&selected))) {
            PWSTR path = nullptr;
            if (SUCCEEDED(selected->GetDisplayName(SIGDN_FILESYSPATH, &path))) {
                SetWindowTextW(destinationBox, path);
                CoTaskMemFree(path);
            }
            selected->Release();
        }
    }
    picker->Release();
}
static void layout_file_list() {
    const bool hasList = (footer.flags & sfx::ShowFiles) != 0;
    const int progressY = hasList ? (filesExpanded ? 430 : 247) : 209;
    const int buttonsY = hasList ? (filesExpanded ? 458 : 275) : 237;
    const int clientHeight = buttonsY + 82;
    if (hasList) {
        SetWindowTextW(filesToggle, filesExpanded ? text(IDS_FILES_EXPANDED).c_str() : text(IDS_FILES_COLLAPSED).c_str());
        ShowWindow(filesBox, filesExpanded ? SW_SHOW : SW_HIDE);
    }
    MoveWindow(progressBar, scale(142), scale(progressY) + messageExtra, scale(486), scale(12), TRUE);
    MoveWindow(progressLabel, scale(640), scale(progressY - 4) + messageExtra, scale(52), scale(20), TRUE);
    MoveWindow(extractButton, scale(562), scale(buttonsY) + messageExtra, scale(130), scale(36), TRUE);
    if (readmeButton) MoveWindow(readmeButton, scale(142), scale(buttonsY) + messageExtra, scale(130), scale(36), TRUE);
    MoveWindow(copyrightLink, scale(142), scale(buttonsY + 46) + messageExtra, scale(550), scale(20), TRUE);
    RECT client{}, outer{};
    GetClientRect(windowHandle, &client);
    GetWindowRect(windowHandle, &outer);
    const int desiredHeight = scale(clientHeight) + messageExtra;
    if (client.bottom != desiredHeight)
        SetWindowPos(windowHandle, nullptr, 0, 0, outer.right - outer.left,
            outer.bottom - outer.top + desiredHeight - client.bottom,
            SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
    InvalidateRect(windowHandle, nullptr, TRUE);
}
static LRESULT CALLBACK window_proc(HWND hwnd, UINT message, WPARAM wp, LPARAM lp) {
    if (message == requestNextPassword) {
        MessageBoxW(hwnd, text(IDS_MORE_PASSWORDS).c_str(), L"ZipGenius SFX", MB_OK | MB_ICONINFORMATION);
        return ask_password() ? TRUE : FALSE;
    }
    if (message == WM_NOTIFY && reinterpret_cast<NMHDR*>(lp)->hwndFrom == copyrightLink &&
        (reinterpret_cast<NMHDR*>(lp)->code == NM_CLICK || reinterpret_cast<NMHDR*>(lp)->code == NM_RETURN)) {
        ShellExecuteW(hwnd, L"open", L"https://zipgenius.it", nullptr, nullptr, SW_SHOWNORMAL);
        return 0;
    }
    if (message == extractionProgress) {
        const int position = std::clamp(static_cast<int>(wp), 0, 100);
        if (extracting && position > SendMessageW(progressBar, PBM_GETPOS, 0, 0)) {
            SendMessageW(progressBar, PBM_SETPOS, position, 0);
            wchar_t label[8];
            wsprintfW(label, L"%d%%", position);
            SetWindowTextW(progressLabel, label);
        }
        return 0;
    }
    if (message == extractionFinished) {
        std::unique_ptr<ExtractionResult> outcome(reinterpret_cast<ExtractionResult*>(lp));
        extracting = false;
        ShowWindow(progressBar, SW_HIDE);
        ShowWindow(progressLabel, SW_HIDE);
        if (outcome->success) {
            if (footer.flags & sfx::CloseOnSuccess) DestroyWindow(hwnd);
            else MessageBoxW(hwnd, text(IDS_EXTRACTED).c_str(), L"ZipGenius SFX", MB_OK | MB_ICONINFORMATION);
        } else if (!outcome->error.empty()) {
            MessageBoxW(hwnd, outcome->error.c_str(), L"ZipGenius SFX", MB_OK | MB_ICONERROR);
        }
        if (IsWindow(hwnd)) {
            EnableWindow(extractButton, TRUE);
            EnableWindow(destinationBox, TRUE);
            EnableWindow(browseButton, TRUE);
            if (readmeButton) EnableWindow(readmeButton, TRUE);
        }
        return 0;
    }
    if (message == WM_ERASEBKGND) return 1;
    if (message == WM_CTLCOLORSTATIC) {
        HDC dc = reinterpret_cast<HDC>(wp);
        SetBkMode(dc, TRANSPARENT);
        SetTextColor(dc, reinterpret_cast<HWND>(lp) == messageControl ? RGB(91, 99, 111) : RGB(35, 43, 54));
        return reinterpret_cast<LRESULT>(reinterpret_cast<HWND>(lp) == iconControl ? sideBrush : whiteBrush);
    }
    if (message == WM_CTLCOLOREDIT || message == WM_CTLCOLORLISTBOX) {
        HDC dc = reinterpret_cast<HDC>(wp);
        SetBkColor(dc, RGB(255, 255, 255));
        SetTextColor(dc, RGB(35, 43, 54));
        return reinterpret_cast<LRESULT>(whiteBrush);
    }
    if (message == WM_PAINT) {
        PAINTSTRUCT paint{};
        HDC dc = BeginPaint(hwnd, &paint);
        RECT client{}; GetClientRect(hwnd, &client);
        FillRect(dc, &client, whiteBrush);
        RECT sidebar{0, 0, scale(112), client.bottom};
        FillRect(dc, &sidebar, sideBrush);
        HPEN border = CreatePen(PS_SOLID, 1, RGB(217, 223, 230));
        HGDIOBJ oldPen = SelectObject(dc, border);
        HGDIOBJ oldBrush = SelectObject(dc, whiteBrush);
        MoveToEx(dc, scale(112), 0, nullptr); LineTo(dc, scale(112), client.bottom);
        RoundRect(dc, scale(142), scale(153) + messageExtra, scale(586), scale(189) + messageExtra, scale(8), scale(8));
        if (filesExpanded)
            RoundRect(dc, scale(142), scale(233) + messageExtra, scale(692), scale(417) + messageExtra, scale(8), scale(8));
        SelectObject(dc, oldBrush); SelectObject(dc, oldPen);
        DeleteObject(border);
        EndPaint(hwnd, &paint);
        return 0;
    }
    if (message == WM_COMMAND) {
        switch (LOWORD(wp)) {
        case 1: extract(); return 0;
        case 2: show_readme(); return 0;
        case 3: if (!fixedDestination) choose_destination(hwnd); return 0;
        case 4: filesExpanded = !filesExpanded; layout_file_list(); return 0;
        }
    }
    if (message == WM_CLOSE) { if (!extracting) DestroyWindow(hwnd); return 0; }
    if (message == WM_DESTROY) { PostQuitMessage(0); return 0; }
    return DefWindowProcW(hwnd, message, wp, lp);
}
int WINAPI wWinMain(HINSTANCE app, HINSTANCE, PWSTR, int) {
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    HRESULT comStatus = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    INITCOMMONCONTROLSEX commonControls{sizeof commonControls, ICC_PROGRESS_CLASS | ICC_LINK_CLASS};
    InitCommonControlsEx(&commonControls);
    instance = app;
    wchar_t self[MAX_PATH];
    GetModuleFileNameW(nullptr, self, MAX_PATH);
    try { if (!load_payload(self)) { MessageBoxW(nullptr, text(IDS_INVALID_PACKAGE).c_str(), L"ZipGenius SFX", MB_ICONERROR); return 1; } }
    catch (...) { MessageBoxW(nullptr, text(IDS_OPEN_FAILED).c_str(), L"ZipGenius SFX", MB_ICONERROR); return 1; }
    if (!resolve_destination()) {
        MessageBoxW(nullptr, text(IDS_FOLDER_UNAVAILABLE).c_str(), L"ZipGenius SFX", MB_ICONERROR);
        std::error_code ignored; fs::remove_all(work, ignored);
        if (SUCCEEDED(comStatus)) CoUninitialize();
        return 1;
    }
    uiDpi = GetDpiForSystem();
    bodyFont = make_font(9, FW_NORMAL);
    headingFont = make_font(17, FW_SEMIBOLD);
    labelFont = make_font(9, FW_SEMIBOLD);
    copyrightFont = make_font(8, FW_BOLD);
    whiteBrush = CreateSolidBrush(RGB(255, 255, 255));
    sideBrush = CreateSolidBrush(RGB(245, 247, 250));
    Gdiplus::GdiplusStartupInput gdiplusInput;
    if (Gdiplus::GdiplusStartup(&gdiplusToken, &gdiplusInput, nullptr) == Gdiplus::Ok)
        sidebarBitmap = render_sidebar_icon();
    if (!sidebarBitmap)
        largeIcon = static_cast<HICON>(LoadImageW(app, MAKEINTRESOURCEW(1), IMAGE_ICON, scale(64), scale(64), LR_DEFAULTCOLOR));
    RECT measure{0, 0, scale(548), 0};
    HDC measureDc = GetDC(nullptr);
    HGDIOBJ previousFont = SelectObject(measureDc, bodyFont);
    DrawTextW(measureDc, footer.message, -1, &measure, DT_CALCRECT | DT_WORDBREAK | DT_NOPREFIX);
    SelectObject(measureDc, previousFont);
    ReleaseDC(nullptr, measureDc);
    messageExtra = std::max(0, static_cast<int>(measure.bottom) + scale(4) - scale(44));
    WNDCLASSW wc{}; wc.lpfnWndProc = window_proc; wc.hInstance = app; wc.lpszClassName = L"ZipGeniusSfxWindow";
    wc.hIcon = LoadIconW(app, MAKEINTRESOURCEW(1)); wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    RegisterClassW(&wc);
    const bool showFiles = (footer.flags & sfx::ShowFiles) != 0;
    filesExpanded = showFiles && (footer.flags & sfx::FilesInitiallyCollapsed) == 0;
    RECT bounds{0, 0, scale(720), scale(showFiles ? (filesExpanded ? 540 : 357) : 319) + messageExtra};
    AdjustWindowRectExForDpi(&bounds, WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX, FALSE, 0, uiDpi);
    windowHandle = CreateWindowW(wc.lpszClassName, footer.archiveName, WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
        CW_USEDEFAULT, CW_USEDEFAULT, bounds.right - bounds.left, bounds.bottom - bounds.top, nullptr, nullptr, app, nullptr);
    int corners = 2;
    DwmSetWindowAttribute(windowHandle, 33, &corners, sizeof corners);
    iconControl = CreateWindowW(L"STATIC", L"", WS_CHILD | WS_VISIBLE | (sidebarBitmap ? SS_BITMAP : SS_ICON),
        scale(24), scale(30), scale(64), scale(64), windowHandle, nullptr, app, nullptr);
    SendMessageW(iconControl, STM_SETIMAGE, sidebarBitmap ? IMAGE_BITMAP : IMAGE_ICON,
        reinterpret_cast<LPARAM>(sidebarBitmap ? static_cast<HANDLE>(sidebarBitmap) : static_cast<HANDLE>(largeIcon)));
    HWND title = CreateWindowW(L"STATIC", text(IDS_TITLE).c_str(), WS_CHILD | WS_VISIBLE,
        scale(142), scale(27), scale(548), scale(32), windowHandle, nullptr, app, nullptr);
    apply_font(title, headingFont);
    messageControl = CreateWindowW(L"STATIC", footer.message, WS_CHILD | WS_VISIBLE | SS_LEFT,
        scale(142), scale(68), scale(548), scale(44) + messageExtra, windowHandle, nullptr, app, nullptr);
    apply_font(messageControl);
    HWND pathLabel = CreateWindowW(L"STATIC", text(IDS_DESTINATION).c_str(), WS_CHILD | WS_VISIBLE,
        scale(142), scale(127) + messageExtra, scale(400), scale(21), windowHandle, nullptr, app, nullptr);
    apply_font(pathLabel, labelFont);
    destinationBox = CreateWindowW(L"EDIT", L"", WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL | (fixedDestination ? ES_READONLY : 0),
        scale(152), scale(160) + messageExtra, scale(fixedDestination ? 530 : 424), scale(24), windowHandle, nullptr, app, nullptr);
    apply_font(destinationBox);
    if (!fixedDestination) {
        browseButton = CreateWindowW(L"BUTTON", text(IDS_BROWSE).c_str(), WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
            scale(598), scale(153) + messageExtra, scale(94), scale(36), windowHandle, reinterpret_cast<HMENU>(3), app, nullptr);
        apply_font(browseButton);
        SetWindowTheme(browseButton, L"Explorer", nullptr);
        wchar_t documents[MAX_PATH];
        if (SUCCEEDED(SHGetFolderPathW(nullptr, CSIDL_PERSONAL, nullptr, 0, documents))) SetWindowTextW(destinationBox, documents);
    } else SetWindowTextW(destinationBox, resolvedDestination.c_str());
    if (showFiles) {
        filesToggle = CreateWindowW(L"BUTTON", L"", WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
            scale(142), scale(198) + messageExtra, scale(550), scale(30),
            windowHandle, reinterpret_cast<HMENU>(4), app, nullptr);
        apply_font(filesToggle, labelFont);
        SetWindowTheme(filesToggle, L"Explorer", nullptr);
        filesBox = CreateWindowW(L"LISTBOX", L"", WS_CHILD | WS_VSCROLL | LBS_NOINTEGRALHEIGHT | (filesExpanded ? WS_VISIBLE : 0),
            scale(151), scale(242) + messageExtra, scale(531), scale(166), windowHandle, nullptr, app, nullptr);
        apply_font(filesBox);
        SetWindowTheme(filesBox, L"Explorer", nullptr);
    }
    progressBar = CreateWindowExW(0, PROGRESS_CLASSW, L"", WS_CHILD | PBS_SMOOTH,
        scale(142), scale(showFiles ? 430 : 209) + messageExtra, scale(486), scale(12),
        windowHandle, nullptr, app, nullptr);
    SendMessageW(progressBar, PBM_SETRANGE32, 0, 100);
    progressLabel = CreateWindowW(L"STATIC", L"0%", WS_CHILD,
        scale(640), scale(showFiles ? 426 : 205) + messageExtra, scale(52), scale(20),
        windowHandle, nullptr, app, nullptr);
    apply_font(progressLabel);
    extractButton = CreateWindowW(L"BUTTON", text(IDS_EXTRACT).c_str(), WS_CHILD | WS_VISIBLE | BS_DEFPUSHBUTTON,
        scale(562), scale(showFiles ? 458 : 237) + messageExtra, scale(130), scale(36), windowHandle, reinterpret_cast<HMENU>(1), app, nullptr);
    apply_font(extractButton, labelFont);
    SetWindowTheme(extractButton, L"Explorer", nullptr);
    SYSTEMTIME localTime{};
    GetLocalTime(&localTime);
    std::wstring copyrightText = L"Copyright \u00A9" + std::to_wstring(localTime.wYear) +
        L" The ZipGenius Team - <a href=\"https://zipgenius.it\">https://zipgenius.it</a>";
    copyrightLink = CreateWindowW(WC_LINK, copyrightText.c_str(), WS_CHILD | WS_VISIBLE | WS_TABSTOP,
        scale(142), scale(showFiles ? (filesExpanded ? 504 : 321) : 283) + messageExtra,
        scale(550), scale(20), windowHandle, nullptr, app, nullptr);
    apply_font(copyrightLink, copyrightFont);
    layout_file_list();
    ShowWindow(windowHandle, SW_SHOW);
    if ((footer.flags & sfx::ShowReadme) && ensure_list() && !readmePath.empty())
    {
        readmeButton = CreateWindowW(L"BUTTON", text(IDS_README).c_str(), WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
            scale(142), scale(showFiles ? (filesExpanded ? 458 : 275) : 237) + messageExtra, scale(130), scale(36), windowHandle, reinterpret_cast<HMENU>(2), app, nullptr);
        apply_font(readmeButton);
        SetWindowTheme(readmeButton, L"Explorer", nullptr);
    }
    if (showFiles) {
        if (ensure_list()) for (const auto& file : entries) SendMessageW(filesBox, LB_ADDSTRING, 0, reinterpret_cast<LPARAM>(file.c_str()));
    }
    if (footer.flags & sfx::AutoStart) PostMessageW(windowHandle, WM_COMMAND, 1, 0);
    MSG msg{}; while (GetMessageW(&msg, nullptr, 0, 0) > 0) { TranslateMessage(&msg); DispatchMessageW(&msg); }
    std::error_code ignored; fs::remove_all(work, ignored);
    if (sidebarBitmap) DeleteObject(sidebarBitmap);
    if (largeIcon) DestroyIcon(largeIcon);
    if (gdiplusToken) Gdiplus::GdiplusShutdown(gdiplusToken);
    DeleteObject(bodyFont); DeleteObject(headingFont); DeleteObject(labelFont); DeleteObject(copyrightFont);
    DeleteObject(whiteBrush); DeleteObject(sideBrush);
    if (SUCCEEDED(comStatus)) CoUninitialize();
    return 0;
}
