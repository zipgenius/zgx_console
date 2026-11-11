#pragma once
#include <cstdint>
#include <string>
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>

namespace sfx {
constexpr char magic[8] = {'Z','G','X','S','F','X','1','\0'};
constexpr uint32_t version = 4;
enum Flags : uint32_t { AutoStart = 1, ShowFiles = 2, RunAfter = 4, ShowReadme = 8, CloseOnSuccess = 16, FilesInitiallyCollapsed = 32 };
enum Format : uint32_t { Zip = 1, SevenZip = 2, Rar = 3 };
enum DestinationFolder : uint32_t { Editable = 0, Desktop = 1, Documents = 2, Downloads = 3, Pictures = 4, Music = 5, Videos = 6, Favorites = 7 };
#pragma pack(push, 1)
struct Footer {
    char signature[8];
    uint32_t schema;
    uint32_t flags;
    uint32_t format;
    uint32_t reserved;
    uint64_t archiveOffset;
    uint64_t archiveLength;
    wchar_t runPath[260];
    wchar_t message[320];
    uint32_t destinationFolder;
    wchar_t destinationSubpath[128];
    wchar_t archiveName[260];
};
#pragma pack(pop)
static_assert(sizeof(Footer) == 1980, "Aggiornare anche SfxPackageWriter.cs se cambia il formato SFX");
inline bool valid_relative(const std::wstring& path) {
    if (path.empty() || path.back() == L'/' || path.back() == L'\\' || path[0] == L'/' || path[0] == L'\\' || path.find(L':') != std::wstring::npos) return false;
    size_t start = 0;
    while (start < path.size()) {
        size_t end = path.find_first_of(L"/\\", start);
        if (end == std::wstring::npos) end = path.size();
        auto part = path.substr(start, end - start);
        if (part.empty() || part == L"." || part == L".." || part.back() == L'.' || part.back() == L' ') return false;
        auto stem = part.substr(0, part.find(L'.'));
        for (auto& c : stem) c = towupper(c);
        if (stem == L"CON" || stem == L"PRN" || stem == L"AUX" || stem == L"NUL" ||
            (stem.size() == 4 && (stem.substr(0, 3) == L"COM" || stem.substr(0, 3) == L"LPT") && stem[3] >= L'1' && stem[3] <= L'9')) return false;
        for (wchar_t c : part) if (c < 32 || c == L'<' || c == L'>' || c == L'|' || c == L'"' || c == L'*' || c == L'?') return false;
        start = end + 1;
    }
    return true;
}
inline std::wstring error(DWORD code) {
    wchar_t* p = nullptr;
    FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
        nullptr, code, 0, reinterpret_cast<LPWSTR>(&p), 0, nullptr);
    std::wstring result = p ? p : L"Errore sconosciuto";
    if (p) LocalFree(p);
    return result;
}
}
