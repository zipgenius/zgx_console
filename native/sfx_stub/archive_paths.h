#pragma once
#include "sfx_format.h"
#include <map>

namespace sfx {
// 7-Zip replaces trailing spaces/dots with underscores on Windows. Validate
// that resulting path and reject ambiguous mappings before invoking extraction.
class ArchivePaths {
    struct IgnoreCase {
        bool operator()(const std::wstring& a, const std::wstring& b) const {
            return CompareStringOrdinal(a.c_str(), -1, b.c_str(), -1, TRUE) == CSTR_LESS_THAN;
        }
    };
    std::map<std::wstring, std::wstring, IgnoreCase> sources;
public:
    bool add(const std::wstring& path) {
        std::wstring normalized, original;
        size_t start = 0;
        while (start < path.size()) {
            size_t end = path.find_first_of(L"/\\", start);
            if (end == std::wstring::npos) end = path.size();
            auto part = path.substr(start, end - start);
            if (part.empty() || part == L"." || part == L"..") return false;
            if (!original.empty()) { original += L'\\'; normalized += L'\\'; }
            original += part;
            // Refuse traversal aliases and reserved names even if 7-Zip would
            // sanitize them. Only trailing spaces and dots are accepted here.
            auto trimmed = part;
            while (!trimmed.empty() && (trimmed.back() == L' ' || trimmed.back() == L'.')) trimmed.pop_back();
            if (!valid_relative(trimmed)) return false;
            for (size_t i = trimmed.size(); i < part.size(); ++i) part[i] = L'_';
            normalized += part;
            if (!valid_relative(normalized)) return false;
            auto [it, inserted] = sources.emplace(normalized, original);
            if (!inserted && it->second != original) return false;
            start = end + 1;
        }
        return !normalized.empty();
    }
};
}
