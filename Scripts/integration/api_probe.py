import requests
import json
import sys

BASE_URL = "http://localhost:7001/api"

def test_api_health():
    try:
        response = requests.get(f"http://localhost:7001/api/benchmark")
        print(f"Health Status: {response.status_code}")
    except Exception as e:
        print(f"Health check failed: {e}")

def list_notebooks():
    try:
        response = requests.get(f"{BASE_URL}/notebook")
        if response.ok:
            notebooks = response.json()
            print(f"Found {len(notebooks)} notebooks:")
            for nb in notebooks:
                print(f" - {nb['name']} ({nb['path']})")
        else:
            print(f"Error fetching notebooks: {response.status_code}")
    except Exception as e:
        print(f"Request failed: {e}")

def list_documentation():
    try:
        response = requests.get(f"{BASE_URL}/documentation")
        if response.ok:
            docs = response.json()
            print(f"Found {len(docs)} documentation files:")
            for doc in docs:
                print(f" - {doc['title']} ({doc['path']})")
        else:
            print(f"Error fetching documentation: {response.status_code}")
    except Exception as e:
        print(f"Request failed: {e}")

if __name__ == "__main__":
    print("Guitar Alchemist API Integration Probe")
    test_api_health()
    list_notebooks()
    list_documentation()
