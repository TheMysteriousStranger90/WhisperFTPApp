using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhisperFTPApp.Models;

public class SettingsEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    
    public string BackgroundPathImage { get; set; }  = "/Assets/Image (3).jpg";
}