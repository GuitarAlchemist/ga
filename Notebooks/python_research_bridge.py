import requests
import pandas as pd
import matplotlib.pyplot as plt

# Configuration
API_BASE_URL = "http://localhost:5000/api" # Ajustez selon votre configuration locale

def get_chords_data():
    """Exemple de récupération de données depuis GaApi"""
    try:
        response = requests.get(f"{API_BASE_URL}/chords/search?query=maj7")
        response.raise_for_status()
        return response.json()
    except Exception as e:
        print(f"Erreur lors de la connexion à GaApi: {e}")
        return None

# Analyse
data = get_chords_data()
if data:
    df = pd.DataFrame(data)
    print(f"Chargé {len(df)} accords.")

    # Exemple de visualisation Python
    # df['quality'].value_counts().plot(kind='bar')
    # plt.title("Distribution des qualités d'accords")
    # plt.show()
else:
    print("Assurez-vous que GaApi est lancé (dotnet run --project Apps/ga-server/GaApi)")
