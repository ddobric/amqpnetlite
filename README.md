# amqpnetlite
AMQP.Net Lite is a lightweight AMQP 1.0 library for the .Net Micro Framework, .Net Compact Framework, .Net Framework, .Net Core, Windows Runtime platforms, and Mono. The library includes both a client and listener to enable peer to peer and broker based messaging.

## Features
* Full control of AMQP 1.0 protocol behavior.
* Peer-to-peer and brokered messaging.
* Secure communication via TLS and SASL.
* Sync and async API support.
* Listener APIs to enable wide range of listener applications, including brokers, routers, proxies, and more.
* A lightweight messaging library that runs on all popular .NET and Windows Runtime platforms.

## Supported Platforms
|            | net45 | net40 | net35 | netmf | netcf | win8/wp8 | netcore451/uap | netstandard1.3<sup>4</sup> |
|------------|:-----:|:-----:|:-----:|:-----:|:-----:|:--------:|:----------:|:----------:|
| TLS        |  +    |   +   |   +   |   +<sup>1</sup>  |   +   |    +     |     +      |     +      |
| SASL<sup>2</sup>      |  +    |   +   |   +   |   +   |   +   |    +     |     +      |     +      |
| AMQP Core  |  +    |   +   |   +   |   +   |   +   |    +     |     +      |     +      |
| Txn        |  +    |   +   |       |       |       |          |            |            |
| Async API  |  +    |   +<sup>3</sup>   |       |       |       |    +     |     +      |     +      |
| Listener   |  +    |   +   |       |       |       |          |            |     +      |
| Serializer |  +    |   +   |   +   |       |       |          |            |     +      |
| WebSockets |  +    |       |       |       |       |          |            |     +<sup>5</sup>      |
| Buffer Pooling |  +    |   +   |       |       |       |          |            |     +      |

1. requires a TLS-capable device.
2. only SASL PLAIN, EXTERNAL, and ANONYMOUS are currently supported.
3. requires Microsoft.Bcl.Async.
4. pre-release. (v1.1.9-rc).
5. support WebSocket client but not listener.

## Tested Platforms
* .Net Framework 3.5, 4.0 and 4.5+.
* .NET Micro Framework 4.2, 4.3, 4.4.
* .NET Compact Framework 3.9.
* Windows Phone 8 and 8.1.
* Windows Store 8 and 8.1. Universal Windows App 10.
* .Net Core 1.0 RC2 on Windows 10 and Ubuntu 14.04.
* Mono on Linux (requires v4.2.1 and up. Only the client APIs are verified and state of the listener APIs is unknown).

## Getting Started
* [Articles and API Documentation](http://azure.github.io/amqpnetlite/)
* [Examples](https://github.com/Azure/amqpnetlite/tree/master/Examples) Please take a minute to look at the examples.
* [.Net Core](https://github.com/Azure/amqpnetlite/tree/master/dotnet) If you are looking for information about using amqpnetlite on .Net Core (coreclr, dnxcore50, etc.), your can find the code and a Hello AMQP! example here.
* Interested in the code?
  * Prerequisites:
    * Visual Studio 2013 (e.g. Community Edition), and optionally
    * NETMF SDK (4.2-4.4) and Visual Studio project system.
    * Application Builder for Windows Embedded Compact 2013.
    * dotnet/cli for building and testing dotnet projects.
    * NuGet tools if you want to build the NuGet package.
  * Visual Studio 2015 is supported but the netcf and win8 projects are excluded because they are not supported by VS2015.
  * If you don't have netmf, netcf, dotnet/cli installed, you can still build the .Net projects in Visual Studio.
  * To run the build.cmd script to do a full build and test, you need all the prerequisites.

## Contributing
This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

If you would like to become an contributor to this project please follow the instructions provided in [Microsoft Azure Projects Contribution Guidelines](http://azure.github.io/guidelines/).

## References
For more information about the Azure Service Bus and AMQP, refer to:
* Azure Service Bus:  http://msdn.microsoft.com/en-us/library/ee732537.aspx. 
* Azure Service Bus and AMQP:  http://msdn.microsoft.com/en-us/library/jj841071.aspx 
* Azure Service Bus Event Hub:  http://azure.microsoft.com/en-us/services/event-hubs/ 
* AMQP:  http://docs.oasis-open.org/amqp/core/v1.0/os/amqp-core-overview-v1.0-os.html

