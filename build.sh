if [ ! -e "paket.lock" ]
then
    exec mono .paket/paket.exe install
fi
dotnet restore src/Api
dotnet build src/Api

dotnet restore tests/Core.Tests
dotnet build tests/Core.Tests
dotnet test tests/Core.Tests
