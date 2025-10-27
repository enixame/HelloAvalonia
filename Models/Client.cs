using System.Collections.Generic;

namespace HelloAvalonia.Models
{
    public class Client
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<Adresse> Adresses { get; set; } = new();
    }
}
