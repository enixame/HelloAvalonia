#!/usr/bin/env python3
import json
import random

# Configuration
NUM_CLIENTS = 100000  # Changez ce nombre pour tester différentes tailles

prenoms = ["Jean", "Marie", "Pierre", "Sophie", "Luc", "Emma", "Thomas", "Julie", "Antoine", "Chloé",
           "Lucas", "Léa", "Hugo", "Camille", "Louis", "Sarah", "Arthur", "Laura", "Gabriel", "Manon"]
noms = ["Dupont", "Martin", "Bernard", "Dubois", "Laurent", "Simon", "Michel", "Lefebvre", "Leroy", "Moreau",
        "Girard", "Roux", "Fournier", "Petit", "Rousseau", "Blanc", "Guerin", "Muller", "Henry", "Roussel"]
types_adresse = ["Domicile", "Travail", "Secondaire", "Facturation", "Livraison"]
rues = ["Rue de la Paix", "Avenue des Champs", "Boulevard Victor Hugo", "Rue de la République", 
        "Place de la Liberté", "Rue Nationale", "Avenue du Général", "Cours Mirabeau",
        "Boulevard de la Mer", "Allée des Roses", "Impasse du Soleil", "Chemin des Vignes"]
villes = ["Paris", "Lyon", "Marseille", "Toulouse", "Nice", "Nantes", "Bordeaux", "Lille", 
          "Strasbourg", "Rennes", "Reims", "Le Havre", "Saint-Étienne", "Toulon", "Grenoble",
          "Dijon", "Angers", "Nîmes", "Villeurbanne", "Saint-Denis"]

print(f"Génération de {NUM_CLIENTS} clients...")

clients = []
for i in range(1, NUM_CLIENTS + 1):
    prenom = random.choice(prenoms)
    nom = random.choice(noms)
    
    client = {
        "Id": i,
        "Nom": f"{prenom} {nom}",
        "Email": f"{prenom.lower()}.{nom.lower()}{i}@example.com",
        "Adresses": []
    }
    
    # Ajouter entre 1 et 5 adresses par client
    num_adresses = random.randint(1, 5)
    for j in range(num_adresses):
        adresse = {
            "Type": random.choice(types_adresse),
            "Rue": f"{random.randint(1, 200)} {random.choice(rues)}",
            "Ville": random.choice(villes),
            "CodePostal": str(random.randint(10000, 99999))
        }
        client["Adresses"].append(adresse)
    
    clients.append(client)
    
    if i % 100 == 0:
        print(f"  {i}/{NUM_CLIENTS} clients générés...")

print(f"\nÉcriture du fichier JSON...")
with open("Data/clients.json", "w", encoding="utf-8") as f:
    json.dump(clients, f, ensure_ascii=False, indent=2)

print(f"✅ Terminé ! {NUM_CLIENTS} clients générés dans Data/clients.json")

# Statistiques
total_adresses = sum(len(c["Adresses"]) for c in clients)
taille_fichier = len(json.dumps(clients, ensure_ascii=False))
print(f"📊 Statistiques:")
print(f"   - Clients: {NUM_CLIENTS}")
print(f"   - Adresses totales: {total_adresses}")
print(f"   - Moyenne d'adresses par client: {total_adresses/NUM_CLIENTS:.1f}")
print(f"   - Taille du fichier: {taille_fichier/1024:.1f} KB ({taille_fichier/1024/1024:.2f} MB)")
