using System.Collections.Generic;
using System.Threading.Tasks;
using WhisperFTPApp.Models;

namespace WhisperFTPApp.Services.Interfaces;

public interface ISettingsService
{
    Task SaveConnectionsAsync(List<FtpConnectionEntity> connections);
    Task<List<FtpConnectionEntity>> LoadConnectionsAsync();
}