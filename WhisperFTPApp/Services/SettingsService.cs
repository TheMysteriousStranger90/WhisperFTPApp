using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WhisperFTPApp.Data;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.Services;

public class SettingsService : ISettingsService
{
    private readonly AppDbContext _context;

    public SettingsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task SaveConnectionsAsync(List<FtpConnectionEntity> connections)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            StaticFileLogger.LogInformation($"[{DateTime.Now:HH:mm:ss}] Saving {connections.Count} connections to database");
            
            var existing = await _context.FtpConnections.ToListAsync();
            if (existing.Any())
            {
                _context.FtpConnections.RemoveRange(existing);
                await _context.SaveChangesAsync();
            }
            
            foreach (var connection in connections)
            {
                var newConnection = new FtpConnectionEntity
                {
                    Name = connection.Name,
                    Address = connection.Address,
                    Username = connection.Username,
                    Password = connection.Password,
                    LastUsed = connection.LastUsed
                };
                
                _context.FtpConnections.Add(newConnection);
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            StaticFileLogger.LogInformation($"[{DateTime.Now:HH:mm:ss}] Connections saved successfully");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            StaticFileLogger.LogError($"[{DateTime.Now:HH:mm:ss}] Database error: {ex.Message}");
            StaticFileLogger.LogError($"[{DateTime.Now:HH:mm:ss}] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    public async Task<List<FtpConnectionEntity>> LoadConnectionsAsync()
    {
        return await _context.FtpConnections
            .Select(e => new FtpConnectionEntity
            {
                Name = e.Name,
                Address = e.Address,
                Username = e.Username,
                Password = e.Password,
                LastUsed = e.LastUsed
            })
            .ToListAsync();
    }
    
    public async Task DeleteConnectionAsync(FtpConnectionEntity connection)
    {
        var entity = await _context.FtpConnections
            .FirstOrDefaultAsync(x => x.Address == connection.Address);
    
        if (entity != null)
        {
            _context.FtpConnections.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
    
    public async Task SaveBackgroundSettingAsync(string backgroundPath)
    {
        var settings = await _context.Settings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new SettingsEntity { BackgroundPathImage = backgroundPath };
            _context.Settings.Add(settings);
        }
        else
        {
            settings.BackgroundPathImage = backgroundPath;
        }
        await _context.SaveChangesAsync();
    }

    public async Task<string> LoadBackgroundSettingAsync()
    {
        var settings = await _context.Settings.FirstOrDefaultAsync();
        return settings?.BackgroundPathImage ?? "/Assets/Image (3).jpg";
    }
}