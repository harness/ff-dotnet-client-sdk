echo ".NET version installed:"
dotnet --version

DOTNET_VERSION=$(dotnet --version | cut -d. -f1)
if [ "$DOTNET_VERSION" -lt 7 ]; then
	echo "FF .NET Client SDK requires .NET 7 or later to build. Aborting"
	exit 1
fi

set -x

dotnet pack ff-dotnet-client-sdk.csproj

# Install Tools needed for build
dotnet tool install --global coverlet.console --version 3.2.0
dotnet tool install --global dotnet-reportgenerator-globaltool
dotnet tool restore

# Install Libraries needed for build and buld
dotnet restore ff-dotnet-client-sdk.csproj
dotnet build ff-dotnet-client-sdk.csproj --no-restore


# Run tests
echo "Generating Test Report"
export MSBUILDDISABLENODEREUSE=1
dotnet test tests/ff-client-sdk-test/ff-client-sdk-test.csproj -v=n --blame-hang --logger:"junit;LogFilePath=junit.xml" -nodereuse:false
ls -l
echo "Done"
