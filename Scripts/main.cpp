#include <fstream>
#include <nlohmann/json.hpp>
#include <iostream>
#include <exception>
#include <curl/curl.h>

size_t WriteCallback(void *content, size_t size, size_t nmemb, void *userp)
{
  size_t totalSize{size * nmemb};
  std::string *buffer = static_cast<std::string *>(userp);
  buffer->append(static_cast<char *>(content), totalSize);
  return totalSize;
}

int main(int argc, char **argv)
{
  if (argc < 2)
  {
    return 0;
  }

  std::ifstream infile(argv[1]);
  if (!infile.is_open())
  {
    throw std::runtime_error("Failed to open repo.json");
    return 1;
  }
  auto repos{nlohmann::ordered_json::parse(infile)};
  CURL *curl;
  CURLcode res;
  std::string response;
  curl = curl_easy_init();
  if (!curl)
  {
    throw std::runtime_error("curl didn't initialized properly");
    return 1;
  }

  for (auto &&repo : repos)
  {
    std::string apiURL{repo["ApiUrl"]};
    curl_easy_setopt(curl, CURLOPT_URL, apiURL.c_str());
    curl_easy_setopt(curl, CURLOPT_USERAGENT, "curl/7.68.0");
    curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, WriteCallback);
    curl_easy_setopt(curl, CURLOPT_WRITEDATA, &response);
    res = curl_easy_perform(curl);

    curl_easy_cleanup(curl);
    if (res == CURLE_OK)
    {
      try
      {
        auto api{nlohmann::json::parse(response)};

        size_t totalDownloadCount{0};
        for (auto &&release : api)
        {
          totalDownloadCount += release["assets"][0]["download_count"].get<int>();
        }
        repo["AssemblyVersion"] = api[0]["tag_name"].get<std::string>();
        repo["DownloadCount"] = totalDownloadCount;
      }
      catch (const std::exception &e)
      {
        std::cerr << e.what() << '\n';
      }
    }
  }
  std::ofstream outfile(argv[1]);
  outfile << repos.dump(2);

  return 0;
}
