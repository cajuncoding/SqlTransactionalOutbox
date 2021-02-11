# SqlTransactionalOutbox
A lightweight library & framework for implementing the Transactional Outbox pattern in .Net with default implementaions for SQL Server & messaging via Azure Service Bus. Some of the key benefits offered are support for running in serverless environments (e.g. AzureFunctions) or in standard hosted .Net applications (via 'worker threads'), and support for enforcing true FIFO processing to preserve ordering, and a simplified abstractions for the Outbox, Outbox Processing, and Messaging systems utilized.

One of the main goals was to offer support for running in serverless environments such as Azure Functions, and the SqlTransactionalOutbox can be easily utilized either way: as hosted .Net Framework/.Net Core application (via 'worker threads'), or as a serverless Azure Functions deployment. Another primary goal of the library is to provide support for enforcing true FIFO processing to preserve ordering as well as providing safe coordination in horizontally scaled environments (e.g. serverless, or load balanced web servers).

The library is completely interface based and extremely modular. In addition, all existing class methods are exposed as virtual methods to make it easy to customize existing implementations as needed, but ultimately we hope that the default implementations will work for the majority of use cases.

##BETA Release v0.0.1
THe library is current being shared/released in a Beta form as we use it for a variety of projects.  As our confidence in the functionality and stability increases through testing. Release notes and detais will be posted here as needed.

##Nuget:
Updae with Nuget Detauls once published.

##TODO:
Provide documentation for:
 - Transactional Outbox Pattern summary/overview
 - Simplified usage of default implementations using easy to consume CustomExtensions.
 - Advanced usage of default implementations with Options
 - Summary of details for customizing impleentations as needed (e.g. Different Publishing implementation)
