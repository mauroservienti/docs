#Configurations

Besides the files, you can also store non-binary data items in the RavenFS. It allows you to store JSON formatted items called *configurations*. 
The usage of .Net Client API allows you to save any object, as under the hood it is serialized to JSON. You will find all the methods to manage configuration items in the [configuration commands](client-api/commands/configurations/set-key).

##Example

This is the example of the configuration item stored under `Raven/Synchronization/Destinations`, which is used internally to handle [a file synchronization](./synchronization/how-it-works):

{CODE-BLOCK:json}
{
    "Destinations": 
	[
		{
			"ServerUrl": "http://localhost:8080",
			"Username": null,
			"Password": null,
			"Domain": null,
			"ApiKey": null,
			"FileSystem": "Northwind",
			"Enabled": true
		}
	]
}
{CODE-BLOCK/}
