#include "archive_paths.h"
#include "password_errors.h"
#include <fstream>
#include <iostream>

int wmain(int argc, wchar_t** argv) {
    int failures = 0, checks = 0;
    auto check = [&](bool ok) { ++checks; if (!ok) ++failures; };
    std::vector<std::string> failed;
    check(sfx::password_failures("ERROR: Wrong password : one.txt\r\nERROR: Data Error in encrypted file. Wrong password? : two.txt\n", failed));
    check(failed == std::vector<std::string>{"one.txt", "two.txt"});
    check(!sfx::password_failures("ERROR: CRC Failed : bad.txt\n", failed));
    check(!sfx::password_failures("ERROR: Wrong password : one.txt\nERROR: Cannot open output file : two.txt\n", failed));
    check(!sfx::password_failures("Everything is Ok\n", failed));
    for (auto path : {L"mail/attachment ", L"mail/file.", L"folder /file.txt", L"normal/file.txt"}) {
        sfx::ArchivePaths paths;
        check(paths.add(path));
    }
    for (auto path : {L"../file", L"a/../file", L"/absolute", L"C:/file", L"a//b", L"CON ", L"NUL.txt", L"a/<file", L"a/.. ", L"a/ .", L"a/stream:data"}) {
        sfx::ArchivePaths paths;
        check(!paths.add(path));
    }
    {
        sfx::ArchivePaths paths;
        check(paths.add(L"folder /one.txt"));
        check(!paths.add(L"folder_/two.txt"));
    }
    {
        sfx::ArchivePaths paths;
        check(paths.add(L"file "));
        check(!paths.add(L"FILE_"));
    }
    if (argc == 2) {
        std::ifstream input(argv[1], std::ios::binary);
        check(bool(input));
        sfx::ArchivePaths paths;
        std::string line;
        bool payload = false;
        while (std::getline(input, line)) {
            if (line.starts_with("----------")) { payload = true; continue; }
            if (!payload || !line.starts_with("Path = ")) continue;
            line.erase(0, 7);
            if (!line.empty() && line.back() == '\r') line.pop_back();
            int size = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, line.data(), static_cast<int>(line.size()), nullptr, 0);
            std::wstring name(size, L'\0');
            MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, line.data(), static_cast<int>(line.size()), name.data(), size);
            while (!name.empty() && (name.back() == L'/' || name.back() == L'\\')) name.pop_back();
            check(paths.add(name));
        }
    }
    std::cout << checks << " checks, " << failures << " failures\n";
    return failures ? 1 : 0;
}
