#pragma once
#include <string>
#include <vector>

namespace sfx {
// Only retry errors explicitly attributed to a password. Other extraction
// errors must fail, so a partial or damaged archive is never reported complete.
inline bool password_failures(const std::string& output, std::vector<std::string>& files) {
    files.clear();
    size_t position = 0;
    while (position < output.size()) {
        auto end = output.find('\n', position);
        auto line = output.substr(position, end == std::string::npos ? end : end - position);
        if (!line.empty() && line.back() == '\r') line.pop_back();
        auto error = line.find("ERROR: ");
        if (error != std::string::npos) {
            line.erase(0, error + 7);
            auto separator = line.find(" : ");
            if (separator == std::string::npos || line.substr(0, separator).find("Wrong password") == std::string::npos)
                return false;
            auto name = line.substr(separator + 3);
            if (name.empty()) return false;
            files.push_back(name);
        }
        if (end == std::string::npos) break;
        position = end + 1;
    }
    return !files.empty();
}
}
