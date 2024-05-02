### Running the example with Dotnet already installed
```bash
export FF_API_KEY=<your key here>
dotnet run --project examples/getting_started/
```

### Running the example with Docker

```bash
docker run -v $(pwd):/app -w /app -e FF_API_KEY=$FF_API_KEY mcr.microsoft.com/dotnet/sdk:8.0 dotnet run --framework net8.0 --project examples/getting_started/
```
