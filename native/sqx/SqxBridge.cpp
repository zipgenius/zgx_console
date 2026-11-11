#include <windows.h>
#include <shellapi.h>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <string>
#include "sqx.h"

#ifdef _M_IX86
#pragma comment(linker, "/EXPORT:SqxExtractArchive=_SqxExtractArchive@12")
#pragma comment(linker, "/EXPORT:SqxCreateArchive=_SqxCreateArchive@28")
#pragma comment(linker, "/EXPORT:RunSqxExtract=_RunSqxExtract@16")
#pragma comment(linker, "/EXPORT:RunSqxCompress=_RunSqxCompress@16")
#endif

#pragma pack(push, 1)
struct SQX_EXTRACTOPTIONSW
{
    DWORD cbSize;
    wchar_t szPassword[MAX_PATH];
    wchar_t szPasswordHeader[MAX_PATH];
    wchar_t szTempDir[MAX_PATH];
    wchar_t szRelativePath[MAX_PATH];
    wchar_t szOutputPath[MAX_PATH];
    BOOL fResetArchiveAttribute;
    BOOL fOverwriteAlways;
    BOOL fKeepBrokenFiles;
    BOOL fCreateFolders;
    BOOL fRestoreACLs;
    BOOL fRestoreStreams;
    BOOL fRestoreUnicodeNames;
    BOOL fRestoreWin32FileTimes;
    BOOL fRestoreDirectoryTimeStamps;
    BOOL fAutoRestoreComments;
    BOOL fSetZoneID;
};
#pragma pack(pop)

typedef int(__stdcall* SqxInitArchiveW)(LPCWSTR, SQXCALLBACK, LPVOID, HANDLE*);
typedef void(__stdcall* SqxDoneArchiveW)(HANDLE);
typedef HANDLE(__stdcall* SqxInitFileListW)(HANDLE);
typedef void(__stdcall* SqxAppendFileListW)(HANDLE, HANDLE, LPCWSTR);
typedef void(__stdcall* SqxDoneFileListW)(HANDLE, HANDLE);
typedef int(__stdcall* SqxExtractFilesW)(HANDLE, SQXCALLBACK, LPVOID, HANDLE, SQX_EXTRACTOPTIONSW*);

static int SQX_API ContinueCallback(LPVOID, LPSQX_CALLBACKINFO callbackInfo)
{
    if (callbackInfo && callbackInfo->dwCallbackType == SQX_CALLBACK_REPLACE)
        return SQX_REPLACE_OVERWRITE;

    return SQX_PROGRESS_OK;
}

static std::wstring GetBridgeDirectory()
{
    HMODULE module = nullptr;
    GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        reinterpret_cast<LPCWSTR>(&GetBridgeDirectory), &module);

    wchar_t modulePath[MAX_PATH];
    GetModuleFileNameW(module, modulePath, MAX_PATH);
    std::wstring directory(modulePath);
    directory.erase(directory.find_last_of(L"\\/") + 1);
    return directory;
}

static std::string ToAnsi(const std::wstring& value)
{
    if (value.empty())
        return std::string();

    int length = WideCharToMultiByte(CP_ACP, 0, value.c_str(), -1, nullptr, 0, nullptr, nullptr);
    if (length <= 1)
        return std::string();

    std::string result(length - 1, '\0');
    WideCharToMultiByte(CP_ACP, 0, value.c_str(), -1, result.data(), length, nullptr, nullptr);
    return result;
}

static void WriteStatusFile(LPCWSTR statusPath, int result)
{
    if (!statusPath)
        return;

    std::wofstream statusFile(statusPath, std::ios::trunc);
    if (statusFile)
        statusFile << result;
}

extern "C" __declspec(dllexport) int __stdcall SqxExtractArchive(LPCWSTR archivePath, LPCWSTR outputPath, LPCWSTR password)
{
    if (!archivePath || !outputPath)
        return 2;

    HMODULE library = LoadLibraryW((GetBridgeDirectory() + L"sqx20u.dll").c_str());
    if (!library)
        return 3;

    SqxInitArchiveW initArchive = reinterpret_cast<SqxInitArchiveW>(GetProcAddress(library, "SqxInitArchive"));
    SqxDoneArchiveW doneArchive = reinterpret_cast<SqxDoneArchiveW>(GetProcAddress(library, "SqxDoneArchive"));
    SqxInitFileListW initFileList = reinterpret_cast<SqxInitFileListW>(GetProcAddress(library, "SqxInitFileList"));
    SqxAppendFileListW appendFileList = reinterpret_cast<SqxAppendFileListW>(GetProcAddress(library, "SqxAppendFileList"));
    SqxDoneFileListW doneFileList = reinterpret_cast<SqxDoneFileListW>(GetProcAddress(library, "SqxDoneFileList"));
    SqxExtractFilesW extractFiles = reinterpret_cast<SqxExtractFilesW>(GetProcAddress(library, "SqxExtractFiles"));
    if (!initArchive || !doneArchive || !initFileList || !appendFileList || !doneFileList || !extractFiles)
    {
        FreeLibrary(library);
        return 4;
    }

    HANDLE archive = nullptr;
    if (initArchive(archivePath, ContinueCallback, nullptr, &archive) != 0 || !archive)
    {
        FreeLibrary(library);
        return 5;
    }

    HANDLE fileList = initFileList(archive);
    if (!fileList)
    {
        doneArchive(archive);
        FreeLibrary(library);
        return 6;
    }

    appendFileList(archive, fileList, L"*.*");
    SQX_EXTRACTOPTIONSW options = {};
    options.cbSize = sizeof(options);
    if (password)
        wcsncpy_s(options.szPassword, password, _TRUNCATE);
    wcsncpy_s(options.szOutputPath, outputPath, _TRUNCATE);
    options.fOverwriteAlways = TRUE;
    options.fCreateFolders = TRUE;
    options.fRestoreUnicodeNames = TRUE;

    int result = extractFiles(archive, ContinueCallback, nullptr, fileList, &options);
    doneFileList(archive, fileList);
    doneArchive(archive);
    FreeLibrary(library);
    return result;
}

extern "C" __declspec(dllexport) int __stdcall SqxCreateArchive(LPCWSTR archivePath, LPCWSTR sourceFolder, int compressionRate, BOOL solidCompression, unsigned long long volumeSize, LPCWSTR password)
{
    if (!archivePath || !sourceFolder)
        return 2;

    std::filesystem::path archive = std::filesystem::absolute(archivePath);
    std::filesystem::path sourceRoot = std::filesystem::absolute(sourceFolder);
    if (!std::filesystem::exists(sourceRoot) || !std::filesystem::is_directory(sourceRoot))
        return 3;

    if (compressionRate < SQX_COMPRATE_STORED)
        compressionRate = SQX_COMPRATE_STORED;
    if (compressionRate > SQX_COMPRATE_ULTRA)
        compressionRate = SQX_COMPRATE_ULTRA;

    HMODULE library = LoadLibraryW((GetBridgeDirectory() + L"sqx20.dll").c_str());
    if (!library)
        return 4;

    SqxInitArchive initArchive = reinterpret_cast<SqxInitArchive>(GetProcAddress(library, "SqxInitArchive"));
    SqxDoneArchive doneArchive = reinterpret_cast<SqxDoneArchive>(GetProcAddress(library, "SqxDoneArchive"));
    SqxInitFileList initFileList = reinterpret_cast<SqxInitFileList>(GetProcAddress(library, "SqxInitFileList"));
    SqxAppendFileList appendFileList = reinterpret_cast<SqxAppendFileList>(GetProcAddress(library, "SqxAppendFileList"));
    SqxDoneFileList doneFileList = reinterpret_cast<SqxDoneFileList>(GetProcAddress(library, "SqxDoneFileList"));
    SqxCompressFiles compressFiles = reinterpret_cast<SqxCompressFiles>(GetProcAddress(library, "SqxCompressFiles"));
    if (!initArchive || !doneArchive || !initFileList || !appendFileList || !doneFileList || !compressFiles)
    {
        FreeLibrary(library);
        return 5;
    }

    std::filesystem::path archiveDirectory = archive.parent_path();
    std::string archiveFileName = ToAnsi(archive.filename().wstring());
    if (archiveFileName.empty())
    {
        FreeLibrary(library);
        return 6;
    }

    wchar_t previousDirectory[MAX_PATH];
    GetCurrentDirectoryW(MAX_PATH, previousDirectory);
    if (!archiveDirectory.empty())
        SetCurrentDirectoryW(archiveDirectory.c_str());
    DeleteFileA(archiveFileName.c_str());

    HANDLE archiveHandle = nullptr;
    int initResult = initArchive(archiveFileName.c_str(), ContinueCallback, nullptr, &archiveHandle);
    if (initResult != SQX_ERR_SUCCESS || !archiveHandle)
    {
        SetCurrentDirectoryW(previousDirectory);
        FreeLibrary(library);
        return initResult != SQX_ERR_SUCCESS ? initResult : 7;
    }

    HANDLE fileList = initFileList(archiveHandle);
    if (!fileList)
    {
        doneArchive(archiveHandle);
        SetCurrentDirectoryW(previousDirectory);
        FreeLibrary(library);
        return 8;
    }

    int fileCount = 0;
    for (const auto& item : std::filesystem::recursive_directory_iterator(sourceRoot))
    {
        if (!item.is_regular_file())
            continue;

        std::wstring relative = std::filesystem::relative(item.path(), sourceRoot).wstring();
        for (wchar_t& ch : relative)
        {
            if (ch == L'/')
                ch = L'\\';
        }

        std::string fileName = ToAnsi(relative);
        if (!fileName.empty())
        {
            appendFileList(archiveHandle, fileList, fileName.c_str());
            fileCount++;
        }
    }

    if (fileCount == 0)
    {
        doneFileList(archiveHandle, fileList);
        doneArchive(archiveHandle);
        SetCurrentDirectoryW(previousDirectory);
        FreeLibrary(library);
        return 9;
    }

    SQX_COMPRESSOPTIONS options = {};
    options.cbSize = sizeof(options);
    options.dwFileFormat = SQX_FILEFORMAT_20;
    options.dwEncryption = password && password[0] != L'\0' ? SQX_ENCRYPTION_AES_256BIT : SQX_ENCRYPTION_NONE;
    if (password)
        strncpy_s(options.szPassword, ToAnsi(password).c_str(), _TRUNCATE);
    options.dwFileNames = SQX_FILENAME_ANSI;
    options.compOptions.dwCompRate = static_cast<DWORD>(compressionRate);
    options.compOptions.fSolidFlag = solidCompression ? TRUE : FALSE;
    options.compOptions.dwDictionarySize = SQX_DICTIONARY_4M;
    options.compOptions.dwExeCompression = SQX_SPECIALCOMP_AUTOMATIC;
    options.compOptions.dwTextCompression = SQX_SPECIALCOMP_AUTOMATIC;
    options.compOptions.dwMultimediaCompression = SQX_SPECIALCOMP_AUTOMATIC;
    options.compOptions.dwAudioCompression = SQX_SPECIALCOMP_AUTOMATIC;
    options.compOptions.dwRgbCompression = SQX_SPECIALCOMP_AUTOMATIC;
    options.fRetainFolderStructure = TRUE;
    options.fStoreWin32FileTime = TRUE;
    options.dwlVolumeSize = static_cast<DWORDLONG>(volumeSize);
    options.fAutoVolumeSize = FALSE;
    strncpy_s(options.szInputPath, ToAnsi(sourceRoot.wstring()).c_str(), _TRUNCATE);

    wchar_t tempPath[MAX_PATH];
    GetTempPathW(MAX_PATH, tempPath);
    strncpy_s(options.szTempDir, ToAnsi(tempPath).c_str(), _TRUNCATE);

    int result = compressFiles(archiveHandle, ContinueCallback, nullptr, fileList, &options);
    doneFileList(archiveHandle, fileList);
    doneArchive(archiveHandle);
    SetCurrentDirectoryW(previousDirectory);
    FreeLibrary(library);
    return result;
}

extern "C" __declspec(dllexport) void CALLBACK RunSqxExtract(HWND, HINSTANCE, LPSTR commandLine, int)
{
    int result = 2;
    int wideLength = MultiByteToWideChar(CP_ACP, 0, commandLine, -1, nullptr, 0);
    if (wideLength > 0)
    {
        std::wstring fullCommandLine(L"SqxBridge ");
        std::wstring arguments(wideLength, L'\0');
        MultiByteToWideChar(CP_ACP, 0, commandLine, -1, arguments.data(), wideLength);
        fullCommandLine += arguments.c_str();

        int argumentCount = 0;
        LPWSTR* argumentValues = CommandLineToArgvW(fullCommandLine.c_str(), &argumentCount);
        if (argumentValues && argumentCount == 5)
        {
            result = SqxExtractArchive(argumentValues[1], argumentValues[2], argumentValues[3]);
            WriteStatusFile(argumentValues[4], result);
        }
        LocalFree(argumentValues);
    }
}

extern "C" __declspec(dllexport) void CALLBACK RunSqxCompress(HWND, HINSTANCE, LPSTR commandLine, int)
{
    int result = 2;
    int wideLength = MultiByteToWideChar(CP_ACP, 0, commandLine, -1, nullptr, 0);
    if (wideLength > 0)
    {
        std::wstring fullCommandLine(L"SqxBridge ");
        std::wstring arguments(wideLength, L'\0');
        MultiByteToWideChar(CP_ACP, 0, commandLine, -1, arguments.data(), wideLength);
        fullCommandLine += arguments.c_str();

        int argumentCount = 0;
        LPWSTR* argumentValues = CommandLineToArgvW(fullCommandLine.c_str(), &argumentCount);
        if (argumentValues && argumentCount == 8)
        {
            int compressionRate = _wtoi(argumentValues[3]);
            BOOL solidCompression = _wtoi(argumentValues[4]) != 0;
            unsigned long long volumeSize = _wcstoui64(argumentValues[5], nullptr, 10);
            result = SqxCreateArchive(argumentValues[1], argumentValues[2], compressionRate, solidCompression, volumeSize, argumentValues[6]);
            WriteStatusFile(argumentValues[argumentCount - 1], result);
        }
        LocalFree(argumentValues);
    }
}
