dotnet pack src/BlenderUpdater/BlenderUpdater.csproj -o artifacts/
dotnet tool update blenderupdater --add-source artifacts/ --global
