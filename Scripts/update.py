import requests
try:
    with open("repo.json") as file:
    except:
response = requests.get("https://api.github.com/repos/Fanazador/AutoPartyFinderRefresher/releases?per_page=100")

if response.status_code == 200:
  data = response.json()
  print("URL: ", data[0]["url"])
else:
  print("Request failed with status: ", response.status_code)
