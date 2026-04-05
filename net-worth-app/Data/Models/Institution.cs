using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace net_worth_app.Data.Models;

[Index(nameof(Name), IsUnique = true)]
public class Institution
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
