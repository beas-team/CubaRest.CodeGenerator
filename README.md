# CubaRest.Codegenerator

> Codegenerator for [Cuba](https://www.cuba-platform.com/)'s project-specific client-side model, based on [CubaRest](https://github.com/beas-team/CubaRest) library.

### Prerequisites
* C# 7
* Add [RestSharp](https://github.com/restsharp/RestSharp) as NuGet package
* [CubaRest](https://github.com/beas-team/CubaRest)
* [Cuba](https://www.cuba-platform.com/) 6.8 or higher at server side

## Configuration

You need a valid REST API connection to run tests.
Rename RestApiConnection_Template.json file to RestApiConnection.json and put there your REST API endpoint and connection credentials.

In order to proceed codegeneration you need to set:
* root namespace for your target assembly
* prefixes for Cuba metaclasses you are interested in
* prefix for you Cuba custom enums
Rename ProjectConfiguration_Template.json file to ProjectConfiguration.json and set the foregoing there.

## Usage example

Run the application. It will create "Model" subfolder in output folder (/bin/Debug) and put generated classes there.

## Built With
* [RestSharp](https://github.com/restsharp/RestSharp)

## Tests

Consider using [CubaRest.Tests](https://github.com/beas-team/CubaRest.Tests) to check matching of client-side classes and enums to server-side types and enums.

## License

This project is licensed under the Apache License 2.0.

## Meta

Sergey Larionov

https://github.com/Zidar