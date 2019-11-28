using System;

namespace ThinkerThings.Services.Account.Api.Models
{
    public class Account
    {
        public int AccountId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public DateTime CreatedAt => DateTime.Now;
    }
}