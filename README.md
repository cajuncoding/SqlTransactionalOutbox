# SqlTransactionalOutbox
A lightweight library for implementing the Transactional Outbox pattern in .Net with default implementaions for SQL Server & messaging via Azure Service Bus. 
One of the main goals was to offer support for running in serverless environments (without necessary 'worker threads') and the SqlTransactionalOutbox can be easily consumed either way: as hosted .Net Framework/.Net Core, or as a server-less Azure Functions deployment. Another primary goal of the library is to provide support for enforcing true FIFO processing to preserve ordering. 

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
