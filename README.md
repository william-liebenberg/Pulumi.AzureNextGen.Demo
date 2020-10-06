# Pulumi.AzureNextGen.Demo

Example of using the [Pulumi Azure.NextGen provider](https://www.pulumi.com/docs/reference/pkg/azure-nextgen/) to create the following resources:
- AppService Plan
- AppService (webapp)
- Cosmos DB (SQL API) Account and Database with Auto-Scaling
- Cosmos DB Container

The Cosmos DB Connection String is added to the AppService's Connection Strings automatically.

To deploy this stack, simply run `pulumi up`
