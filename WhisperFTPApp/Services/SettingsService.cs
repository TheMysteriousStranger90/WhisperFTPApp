using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WhisperFTPApp.Data;
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
        try
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Saving {connections.Count} connections to database");
            _context.FtpConnections.RemoveRange(_context.FtpConnections);
            await _context.FtpConnections.AddRangeAsync(connections);
            await _context.SaveChangesAsync();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Connections saved successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Database error: {ex.Message}");
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
                LastUsed = e.LastUsed
            })
            .ToListAsync();
    }
}