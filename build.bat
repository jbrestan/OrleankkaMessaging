IF NOT EXIST paket.lock (
    START /WAIT .paket/paket.exe install
)
dotnet restore src/Api
dotnet build src/Api

dotnet restore tests/Core.Tests
dotnet build tests/Core.Tests
dotnet test tests/Core.Tests
