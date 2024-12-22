using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhisperFTPApp.Models;

public class FtpConnectionEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string Name { get; set; }
    public string Address { get; set; }
    public string Username { get; set; }
    public DateTime LastUsed { get; set; }
}