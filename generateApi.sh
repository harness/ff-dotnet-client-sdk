# openapi-generator config-help -g csharp  or  https://openapi-generator.tech/docs/generators/csharp/
# use openapi-generator version 7.3.0

rm -rf openapi_tmp openapi
openapi-generator-cli generate -i ./ff-api/docs/release/client-v1.yaml -g csharp -o openapi_tmp -p="packageName=io.harness.ff_dotnet_client_sdk.openapi,library=httpclient,nonPublicApi=true,targetFramework=netstandard2.0,equatable=true"
rm openapi_tmp/src/io.harness.ff_dotnet_client_sdk.openapi/*.csproj
mv openapi_tmp/src/io.harness.ff_dotnet_client_sdk.openapi openapi
rm -rf openapi_tmp
