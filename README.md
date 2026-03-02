# Http Multipart Parser

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://httpmultipartparser.mit-license.org/)
![Sourcelink](https://img.shields.io/badge/sourcelink-enabled-brightgreen.svg)
[![Build status](https://ci.appveyor.com/api/projects/status/t547jmcf10s53h2u?svg=true)](https://ci.appveyor.com/project/Jericho/http-multipart-data-parser)
[![tests](https://img.shields.io/appveyor/tests/jericho/http-multipart-data-parser)](https://ci.appveyor.com/project/jericho/http-multipart-data-parser/build/tests)
[![Coverage Status](https://coveralls.io/repos/github/Http-Multipart-Data-Parser/Http-Multipart-Data-Parser/badge.svg?branch=master)](https://coveralls.io/github/Http-Multipart-Data-Parser/Http-Multipart-Data-Parser?branch=master)
[![CodeFactor](https://www.codefactor.io/repository/github/http-multipart-data-parser/http-multipart-data-parser/badge)](https://www.codefactor.io/repository/github/http-multipart-data-parser/http-multipart-data-parser)

| Release Notes| NuGet (stable) | MyGet (prerelease) |
|--------------|----------------|--------------------|
| [![GitHub release](https://img.shields.io/github/release/http-multipart-data-parser/http-multipart-data-parser.svg)](https://github.com/http-multipart-data-parser/http-multipart-data-parser/releases) | [![Nuget](https://img.shields.io/nuget/v/HttpMultipartParser.svg)](https://www.nuget.org/packages/HttpMultipartParser/) | [![MyGet Pre Release](https://img.shields.io/myget/jericho/vpre/HttpMultipartParser.svg)](http://myget.org/gallery/jericho) |

## About

The Http Multipart Parser does it exactly what it claims on the tin: parses multipart/form-data. This particular
parser is well suited to parsing large data from streams as it doesn't attempt to read the entire stream at once and
procudes a set of streams for file data.

## Installation

The easiest way to include HttpMultipartParser in your project is by adding the nuget package to your project:

```
PM> Install-Package HttpMultipartParser
```

## .NET framework suport

- The parser currently targets `.NET Standard 2.0` and .NET 8.0.
- Version 5.1.0 was the last version that supported .NET 4.6.1, NET 4.7.2 and .NET standard 2.0.
- Version 2.2.4 was the last version that supported older .NET platforms such as .NET 4.5 and .NET standard 1.3.

## Usage

### Non-Streaming (Simple, don't use on very large files)
1. Parse the stream containing the multipart/form-data by invoking `MultipartFormDataParser.Parse` (or it's asynchronous counterpart `MultipartFormDataParser.ParseAsync`).
2. Access the data through the parser.

### Streaming (Handles large files)
1. Create a new StreamingMultipartFormDataParser with the stream containing the multipart/form-data
2. Set up the ParameterHandler and FileHandler delegates
3. Call `parser.Run()` (or it's asynchronous counterpart `parser.RunAsync()`)
4. The delegates will be called as data streams in.

## Examples

### Single file

```
// stream:
-----------------------------41952539122868
Content-Disposition: form-data; name="username"

example
-----------------------------41952539122868
Content-Disposition: form-data; name="email"

example@data.com
-----------------------------41952539122868
Content-Disposition: form-data; name="files[]"; filename="photo1.jpg"
Content-Type: image/jpeg

ExampleBinaryData012031203
-----------------------------41952539122868--
```

```csharp
// ===== Simple Parsing ====
// You can parse synchronously:
var parser = MultipartFormDataParser.Parse(stream);

// Or you can parse asynchronously:
var parser = await MultipartFormDataParser.ParseAsync(stream).ConfigureAwait(false);

// From this point the data is parsed, we can retrieve the
// form data using the GetParameterValue method.
var username = parser.GetParameterValue("username");
var email = parser.GetParameterValue("email");

// Files are stored in a list:
var file = parser.Files.First();
string filename = file.FileName;
Stream data = file.Data;

// ==== Advanced Parsing ====
var parser = new StreamingMultipartFormDataParser(stream);
parser.ParameterHandler += parameter => DoSomethingWithParameter(parameter);
parser.FileHandler += (name, fileName, type, disposition, buffer, bytes, partNumber, additionalProperties) =>
{
    // Write the part of the file we've received to a file stream. (Or do something else)
    filestream.Write(buffer, 0, bytes);
};

// You can parse synchronously:
parser.Run();

// Or you can parse asynchronously:
await parser.RunAsync().ConfigureAwait(false);
```

### Multiple Parameters

```
// stream:
-----------------------------41952539122868
Content-Disposition: form-data; name="checkbox"

likes_cake
-----------------------------41952539122868
Content-Disposition: form-data; name="checkbox"

likes_cookies
-----------------------------41952539122868--
```
```csharp
// ===== Simple Parsing ====
// You can parse synchronously:
var parser = MultipartFormDataParser.Parse(stream);

// Or you can parse asynchronously:
var parser = await MultipartFormDataParser.ParseAsync(stream).ConfigureAwait(false);

// From this point the data is parsed, we can retrieve the
// form data from the GetParameterValues method
var checkboxResponses = parser.GetParameterValues("checkbox");
foreach(var parameter in checkboxResponses)
{
    Console.WriteLine("Parameter {0} is {1}", parameter.Name, parameter.Data)
}
```

### Multiple Files

```
// stream:
-----------------------------41111539122868
Content-Disposition: form-data; name="files[]"; filename="photo1.jpg"
Content-Type: image/jpeg

MoreBinaryData
-----------------------------41111539122868
Content-Disposition: form-data; name="files[]"; filename="photo2.jpg"
Content-Type: image/jpeg

ImagineLotsOfBinaryData
-----------------------------41111539122868--
```
```csharp
// ===== Simple Parsing ====
// You can parse synchronously:
var parser = MultipartFormDataParser.Parse(stream);

// Or you can parse asynchronously:
var parser = await MultipartFormDataParser.ParseAsync(stream).ConfigureAwait(false);

// Loop through all the files
foreach(var file in parser.Files)
{
    Stream data = file.Data;

    // Do stuff with the data.
}

// ==== Advanced Parsing ====
var parser = new StreamingMultipartFormDataParser(stream);
parser.ParameterHandler += parameter => DoSomethingWithParameter(parameter);
parser.FileHandler += (name, fileName, type, disposition, buffer, bytes, partNumber, additionalProperties) =>
{
    // Write the part of the file we've received to a file stream. (Or do something else)
    // Assume that filesreamsByName is a Dictionary<string, FileStream> of all the files
    // we are writing.
    filestreamsByName[name].Write(buffer, 0, bytes);
};
parser.StreamClosedHandler += () 
{
    // Do things when my input stream is closed
};

// You can parse synchronously:
parser.Run();

// Or you can parse asynchronously:
await parser.RunAsync().ConfigureAwait(false);
```
## Licensing

This project is licensed under MIT.
