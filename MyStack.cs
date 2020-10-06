#nullable disable

using Pulumi;
using Pulumi.AzureNextGen.DocumentDB.Latest;
using Pulumi.AzureNextGen.DocumentDB.Latest.Inputs;
using Pulumi.AzureNextGen.Resources.Latest;
using Pulumi.AzureNextGen.Web.Latest;
using Pulumi.AzureNextGen.Web.Latest.Inputs;
using System.Linq;
using System.Threading.Tasks;

class MyStack : Stack
{
	public MyStack()
	{
		Config config = new Config();

		// Create an Azure Resource Group
		var resourceGroup = new ResourceGroup("resourceGroup", new ResourceGroupArgs
		{
			ResourceGroupName = "pulumidemo",
			Location = "AustraliaSouthEast"
		});

		// Create a Cosmos DB Database
		string cosmosAccountName = "mypulumidemo-dev";
		var cosmosAccount = new DatabaseAccount(cosmosAccountName, new DatabaseAccountArgs()
		{
			AccountName = cosmosAccountName,
			ResourceGroupName = resourceGroup.Name,
			Location = resourceGroup.Location,
			ConsistencyPolicy = new ConsistencyPolicyArgs()
			{
				DefaultConsistencyLevel = "Session"
			},
			DatabaseAccountOfferType = "Standard",
			EnableFreeTier = false,
			EnableMultipleWriteLocations = false,
			IsVirtualNetworkFilterEnabled = false,
			Locations =
			{
				new LocationArgs
				{
					FailoverPriority = 0,
					IsZoneRedundant = false,
					LocationName = resourceGroup.Location,
				}
			}
		});

		// Export the CosmosDb Connection String
		this.CosmosDatabaseConnectionString = Output
				.Tuple(resourceGroup.Name, cosmosAccount.Name)
				.Apply(names => Output.CreateSecret(GetCosmosDatabaseConnectionString(names.Item1, names.Item2)));

		// set with > pulumi config set myCosmosDb.autoScaleThroughput 4000
		int autoScaleThroughput = config.RequireInt32("myCosmosDb.autoScaleThroughput");

		string cosmosDatabaseName = "myCosmosDatabase";
		var cosmosSqlDatabase = new SqlResourceSqlDatabase(cosmosDatabaseName, new SqlResourceSqlDatabaseArgs()
		{
			DatabaseName = cosmosDatabaseName,
			AccountName = cosmosAccount.Name,
			ResourceGroupName = resourceGroup.Name,
			Location = resourceGroup.Location,
			Resource = new SqlDatabaseResourceArgs()
			{
				Id = cosmosDatabaseName
			},
			Options = new CreateUpdateOptionsArgs()
			{
				AutoscaleSettings = new AutoscaleSettingsArgs()
				{
					MaxThroughput = autoScaleThroughput
				}
			}
		});

		string rulesContainerName = "rules";
		var rulesContainer = new SqlResourceSqlContainer(rulesContainerName, new SqlResourceSqlContainerArgs
		{
			AccountName = cosmosAccount.Name,
			ContainerName = rulesContainerName,
			DatabaseName = cosmosSqlDatabase.Name,
			ResourceGroupName = resourceGroup.Name,
			Location = resourceGroup.Location,
			Options = new CreateUpdateOptionsArgs() { },
			Resource = new SqlContainerResourceArgs
			{
				Id = rulesContainerName,
				IndexingPolicy = new IndexingPolicyArgs
				{
					Automatic = true,
					IndexingMode = "Consistent",
				},
				PartitionKey = new ContainerPartitionKeyArgs
				{
					Kind = "Hash",
					Paths =
					{
						"/id",
					},
				}
			}
		});

		// Create an AppService Plan (Web Server)
		string appServicePlanName = "myRulesServer";
		var appServicePlan = new AppServicePlan(appServicePlanName, new AppServicePlanArgs
		{
			Name = appServicePlanName,
			Location = resourceGroup.Location,
			ResourceGroupName = resourceGroup.Name,
			Kind = "app",
			Sku = new SkuDescriptionArgs
			{
				Capacity = 1,
				Family = "P",
				Name = "P1",
				Size = "P1",
				Tier = "Premium",
			}
		});

		// Create a WebApp
		string appServiceName = "myRulesApp";
		var appService = new WebApp(appServiceName, new WebAppArgs
		{
			Name = appServiceName,
			Location = resourceGroup.Location,
			ResourceGroupName = resourceGroup.Name,
			ServerFarmId = appServicePlan.Id,
			SiteConfig = new SiteConfigArgs()
			{
				AlwaysOn = true,
				AppSettings = new InputList<NameValuePairArgs>() { },
				ConnectionStrings = new InputList<ConnStringInfoArgs>()
				{
					new ConnStringInfoArgs()
					{
						Name = "RulesDatabase",
						Type = "Custom",
						ConnectionString = Output
							.Tuple(resourceGroup.Name, cosmosAccount.Name)
							.Apply(names => Output.Create(GetCosmosDatabaseConnectionString(names.Item1, names.Item2)))
					}
				}
			}
		});
	}

	[Output]
	public Output<string> CosmosDatabaseConnectionString { get; set; }

	private static async Task<string> GetCosmosDatabaseConnectionString(string resourceGroupName, string cosmosAccountName)
	{
		var connectionStrings = await ListDatabaseAccountConnectionStrings.InvokeAsync(new ListDatabaseAccountConnectionStringsArgs
		{
			ResourceGroupName = resourceGroupName,
			AccountName = cosmosAccountName
		});
		return connectionStrings.ConnectionStrings.First().ConnectionString;
	}
}
