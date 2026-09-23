using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Channels;
using RLE;
using Tommy;

namespace SensorInterface.Sensor;

public interface ISubprocessService : IAsyncDisposable
{
    bool Connected();
    event EventHandler<string>? outputReceived;
    event EventHandler? ProcessExited;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task SendCommandAsync(string command, CancellationToken cancellationToken = default);
}

public enum MessageType
{
    SensorData,
}

public class SensorService : ISubprocessService
{
    readonly string _pipeName, _exePath;
    SensorType sType;

    NamedPipeClientStream? _pipeClient;
    StreamWriter? _writer;
    StreamReader? _reader;
    Process? _process;

    public Channel<FrameRecord> inputQueue;

    public event EventHandler<String> outputReceived;
    public event EventHandler? ProcessExited;
    public SensorService(string pipeName)
    {
        _pipeName = pipeName;
        _exePath = Environment.ProcessPath!;
    }

    public bool Connected() => _pipeClient?.IsConnected ?? false;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _exePath,
                Arguments = "",
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };
        _process.Exited += (s,e) => ProcessExited?.Invoke(this, EventArgs.Empty);
        _process.Start();

        _pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await _pipeClient.ConnectAsync(5000, cancellationToken);

        _writer = new StreamWriter(_pipeClient) { AutoFlush = true };
        _reader = new StreamReader(_pipeClient);

        _ = ReadLoopAsync(cancellationToken);
    }

        public async Task Read<T>(ChannelWriter<T> recorder, StreamReader reader)
    {
        // Read Header
        char[] headerBuf = new char[5];
        await reader.ReadAsync(headerBuf, 0, headerBuf.Length);

        // Use Header to Read the payload
        char[] bodyBuf = new char[UInt32.Parse(headerBuf[1..])];
        await reader.ReadBlockAsync(bodyBuf, 0, bodyBuf.Length);

        switch (headerBuf[0])
        {
            case (char)MessageType.SensorData: 
            // TODO: Use CCAM Sensor Data format Parser
            BinaryLoader.LoadData<T, Array>(new BinaryReader(reader.BaseStream), BinaryLoader.DefaultTagParser, BinaryLoader.DefaultDataParser);
            // TODO: Load into the recorder channel
            // inputQueue.Writer.WriteAsync();
                break;
            
            default:
                break;
        }

    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _reader != null && !_reader.EndOfStream)
        {
            // var line = await _pipeClient.ReadAsync();
            // if (line != null) outputReceived.Invoke(this, line);
        }
    }

    public async Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (_writer != null && Connected()) await _writer.WriteAsync(command.AsMemory(), cancellationToken);
    }

    public async Task SendConfig(TomlTable config, CancellationToken cancellationToken = default)
    {
        await SendCommandAsync(config, cancellationToken); // TODO: Check if this is valid
    }

    public void Connect()
    {
        
    }

    public void Disconnect()
    {
        
    }

    public void Enable()
    {
        // TODO: Send Enable Command
    }

    public void Disable()
    {
        // TODO: Send Disable Command
    }

    public async ValueTask DisposeAsync()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _pipeClient?.Dispose();
        if (_process is {HasExited: false}) _process.Kill();
        _process?.Dispose();
    }
}