using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HelloAvalonia.Models;

namespace HelloAvalonia
{
    public class TestPerformance
    {
        public static void GenerateLargeDataset(int numberOfClients = 10000)
        {
            var clients = new List<Client>();
            var random = new Random();
            
            var types = new[] { "Domicile", "Travail", "Secondaire", "Facturation", "Livraison" };
            var prenoms = new[] { "Jean", "Marie", "Pierre", "Sophie", "Luc", "Emma", "Thomas", "Julie", "Antoine", "Chloé" };
            var noms = new[] { "Dupont", "Martin", "Bernard", "Dubois", "Laurent", "Simon", "Michel", "Lefebvre", "Leroy", "Moreau" };
            var rues = new[] { "Rue de la Paix", "Avenue des Champs", "Boulevard Victor Hugo", "Rue de la République", "Place de la Liberté" };
            var villes = new[] { "Paris", "Lyon", "Marseille", "Toulouse", "Nice", "Nantes", "Bordeaux", "Lille", "Strasbourg", "Rennes" };

            Console.WriteLine($"Génération de {numberOfClients} clients...");
            var startTime = DateTime.Now;

            for (int i = 1; i <= numberOfClients; i++)
            {
                var client = new Client
                {
                    Id = i,
                    Nom = $"{prenoms[random.Next(prenoms.Length)]} {noms[random.Next(noms.Length)]}",
                    Email = $"client{i}@example.com",
                    Adresses = new List<Adresse>()
                };

                // Ajouter entre 1 et 5 adresses par client
                var numAdresses = random.Next(1, 6);
                for (int j = 0; j < numAdresses; j++)
                {
                    client.Adresses.Add(new Adresse
                    {
                        Type = types[random.Next(types.Length)],
                        Rue = $"{random.Next(1, 200)} {rues[random.Next(rues.Length)]}",
                        Ville = villes[random.Next(villes.Length)],
                        CodePostal = $"{random.Next(10000, 99999)}"
                    });
                }

                clients.Add(client);

                if (i % 1000 == 0)
                {
                    Console.WriteLine($"  {i} clients générés...");
                }
            }

            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            Console.WriteLine($"Génération terminée en {elapsed:F2} secondes");

            // Sauvegarder dans un fichier
            var jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "clients_large.json");
            var jsonContent = JsonSerializer.Serialize(clients, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, jsonContent);
            
            Console.WriteLine($"Fichier sauvegardé: {jsonPath} ({jsonContent.Length / 1024 / 1024:F2} MB)");
        }
    }
}
