using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive;
using ReactiveUI;
using WhisperFTPApp.Configurations;
using WhisperFTPApp.Constants;
using WhisperFTPApp.Events;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;
using WhisperFTPApp.Services.Interfaces;
using WhisperFTPApp.Validation;

namespace WhisperFTPApp.ViewModels;

public sealed class ConnectionViewModel : ReactiveObject, IDisposable
{
    private readonly IFtpService _ftpService;
    private readonly ISettingsService _settingsService;
    private readonly ICredentialEncryption _credentialEncryption;

    private string _ftpAddress = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _port = AppConstants.DefaultFtpPort.ToString(CultureInfo.InvariantCulture);
    private bool _isConnected;
    private int _timeout = AppConstants.DefaultTimeout;
    private int _readWriteTimeout = AppConstants.DefaultReadWriteTimeout;
    private bool _enableSsl = true;
    private bool _usePassive = true;
    private int _bufferSize = AppConstants.DefaultBufferSize;
    private int _maxRetries = AppConstants.DefaultMaxRetries;
    private int _retryDelay = AppConstants.DefaultRetryDelay;
    private FtpConnectionEntity? _selectedRecentConnection;
    private readonly ObservableCollection<FtpConnectionEntity> _recentConnections = new();
    private bool _allowInvalidCertificates;

    public ConnectionViewModel(
        IFtpService ftpService,
        ISettingsService settingsService,
        ICredentialEncryption credentialEncryption)
    {
        _ftpService = ftpService;
        _settingsService = settingsService;
        _credentialEncryption = credentialEncryption;

        ConnectCommand = ReactiveCommand.CreateFromTask(
            ConnectAsync,
            this.WhenAnyValue(x => x.IsConnected, connected => !connected));

        DisconnectCommand = ReactiveCommand.CreateFromTask(
            DisconnectAsync,
            this.WhenAnyValue(x => x.IsConnected));

        CleanCommand = ReactiveCommand.Create(CleanFields);
        SaveConnectionCommand = ReactiveCommand.CreateFromTask(SaveSuccessfulConnectionAsync);
        DeleteConnectionCommand = ReactiveCommand.CreateFromTask<FtpConnectionEntity>(DeleteConnectionAsync);
        ShowRecentConnectionsCommand = ReactiveCommand.Create(() => { });

        _ = LoadRecentConnectionsAsync();
    }

    public string FtpAddress
    {
        get => _ftpAddress;
        set => this.RaiseAndSetIfChanged(ref _ftpAddress, value);
    }

    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    public string Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    public int Timeout
    {
        get => _timeout;
        set => this.RaiseAndSetIfChanged(ref _timeout, value);
    }

    public int ReadWriteTimeout
    {
        get => _readWriteTimeout;
        set => this.RaiseAndSetIfChanged(ref _readWriteTimeout, value);
    }

    public bool EnableSsl
    {
        get => _enableSsl;
        set => this.RaiseAndSetIfChanged(ref _enableSsl, value);
    }

    public bool UsePassive
    {
        get => _usePassive;
        set => this.RaiseAndSetIfChanged(ref _usePassive, value);
    }

    public int BufferSize
    {
        get => _bufferSize;
        set => this.RaiseAndSetIfChanged(ref _bufferSize, value);
    }

    public int MaxRetries
    {
        get => _maxRetries;
        set => this.RaiseAndSetIfChanged(ref _maxRetries, value);
    }

    public int RetryDelay
    {
        get => _retryDelay;
        set => this.RaiseAndSetIfChanged(ref _retryDelay, value);
    }

    public bool AllowInvalidCertificates
    {
        get => _allowInvalidCertificates;
        set => this.RaiseAndSetIfChanged(ref _allowInvalidCertificates, value);
    }

    public FtpConnectionEntity? SelectedRecentConnection
    {
        get => _selectedRecentConnection;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedRecentConnection, value);
            if (value != null)
            {
                _ = SwitchConnectionAsync(value);
            }
        }
    }

    public ObservableCollection<FtpConnectionEntity> RecentConnections => _recentConnections;

    public ReactiveCommand<Unit, bool> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<Unit, Unit> CleanCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveConnectionCommand { get; }
    public ReactiveCommand<FtpConnectionEntity, Unit> DeleteConnectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowRecentConnectionsCommand { get; }

    public event EventHandler<StatusChangedEventArgs>? StatusChanged;
    public event EventHandler? ConnectionEstablished;
    public event EventHandler? ConnectionLost;

    public FtpConfiguration CreateConfiguration()
    {
        if (!int.TryParse(Port, out int portNumber))
        {
            throw new InvalidOperationException("Invalid port number");
        }

        var validation = FtpConnectionValidator.Validate(FtpAddress, Username, Password, portNumber);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.ErrorMessage);
        }

        var uri = new Uri(FtpAddress.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)
            ? FtpAddress
            : $"ftp://{FtpAddress}");
        var baseAddress = $"{uri.Scheme}://{uri.Host}";

        return new FtpConfiguration
        {
            FtpAddress = baseAddress,
            Username = Username,
            Password = Password,
            Port = portNumber,
            Timeout = Timeout,
            ReadWriteTimeout = ReadWriteTimeout,
            EnableSsl = EnableSsl,
            UsePassive = UsePassive,
            UseBinary = true,
            KeepAlive = true,
            BufferSize = BufferSize,
            MaxRetries = MaxRetries,
            RetryDelay = RetryDelay,
            AllowInvalidCertificates = AllowInvalidCertificates
        };
    }

    private async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        StaticFileLogger.LogInformation($"Attempting to connect to {FtpAddress}");
        try
        {
            StatusChanged?.Invoke(this, new StatusChangedEventArgs("Connecting to FTP server..."));

            var configuration = CreateConfiguration();
            bool isConnected = await _ftpService.ConnectAsync(configuration, cancellationToken).ConfigureAwait(true);

            if (isConnected)
            {
                IsConnected = true;
                StatusChanged?.Invoke(this, new StatusChangedEventArgs("Connected successfully"));
                StaticFileLogger.LogInformation($"Successfully connected to {FtpAddress}");
                await SaveSuccessfulConnectionAsync(cancellationToken).ConfigureAwait(true);
                ConnectionEstablished?.Invoke(this, EventArgs.Empty);
                return true;
            }

            IsConnected = false;
            StatusChanged?.Invoke(this,
                new StatusChangedEventArgs("Failed to connect. Please check your credentials."));
            StaticFileLogger.LogError($"Failed to connect to {FtpAddress}");
            return false;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusChanged?.Invoke(this, new StatusChangedEventArgs($"Connection error: {ex.Message}"));
            StaticFileLogger.LogError($"Connection error: {ex.Message}");
            return false;
        }
    }

    private async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        StaticFileLogger.LogInformation("Disconnecting from FTP server");
        try
        {
            await _ftpService.DisconnectAsync(cancellationToken).ConfigureAwait(true);
            IsConnected = false;
            StatusChanged?.Invoke(this, new StatusChangedEventArgs("Disconnected from FTP server"));
            StaticFileLogger.LogInformation("Successfully disconnected from FTP server");
            ConnectionLost?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, new StatusChangedEventArgs($"Error disconnecting: {ex.Message}"));
            StaticFileLogger.LogError($"Disconnect failed: {ex.Message}");
        }
    }

    private void CleanFields()
    {
        FtpAddress = string.Empty;
        Username = string.Empty;
        Password = string.Empty;
        Port = AppConstants.DefaultFtpPort.ToString(CultureInfo.InvariantCulture);
        StatusChanged?.Invoke(this, new StatusChangedEventArgs("Fields cleared"));
    }

    private async Task SaveSuccessfulConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = new FtpConnectionEntity
            {
                Name = FtpAddress,
                Address = FtpAddress,
                Username = Username,
                Password = _credentialEncryption.Encrypt(Password),
                LastUsed = DateTime.UtcNow
            };

            var connections = _recentConnections.ToList();
            var existing = connections.FirstOrDefault(c =>
                c.Address.Equals(connection.Address, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                connections.Remove(existing);
            }

            connections.Add(connection);
            var toSave = connections.OrderByDescending(c => c.LastUsed).Take(10).ToList();

            await _settingsService.SaveConnectionsAsync(toSave, cancellationToken).ConfigureAwait(true);
            await LoadRecentConnectionsAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Error saving connection: {ex.Message}");
        }
    }

    private async Task SwitchConnectionAsync(FtpConnectionEntity connection,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsConnected)
            {
                await DisconnectAsync(cancellationToken).ConfigureAwait(true);
            }

            FtpAddress = connection.Address;
            Username = connection.Username;
            Password = _credentialEncryption.Decrypt(connection.Password);
            await ConnectAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, new StatusChangedEventArgs($"Error switching connection: {ex.Message}"));
        }
        finally
        {
            SelectedRecentConnection = null;
        }
    }

    private async Task LoadRecentConnectionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var connections = await _settingsService.LoadConnectionsAsync(cancellationToken).ConfigureAwait(true) ??
                              new List<FtpConnectionEntity>();
            _recentConnections.Clear();
            foreach (var connection in connections.OrderByDescending(c => c.LastUsed))
            {
                _recentConnections.Add(connection);
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, new StatusChangedEventArgs($"Error loading connections: {ex.Message}"));
            _recentConnections.Clear();
        }
    }

    private async Task DeleteConnectionAsync(FtpConnectionEntity connection,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _settingsService.DeleteConnectionAsync(connection, cancellationToken).ConfigureAwait(true);
            _recentConnections.Remove(connection);
            StatusChanged?.Invoke(this, new StatusChangedEventArgs("Connection removed from history"));
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, new StatusChangedEventArgs($"Error removing connection: {ex.Message}"));
        }
    }

    public void Dispose()
    {
        ConnectCommand.Dispose();
        DisconnectCommand.Dispose();
        CleanCommand.Dispose();
        SaveConnectionCommand.Dispose();
        DeleteConnectionCommand.Dispose();
        ShowRecentConnectionsCommand.Dispose();
    }
}
