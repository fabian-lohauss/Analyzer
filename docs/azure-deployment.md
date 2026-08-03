# Azure deployment

The application runs on Azure App Service and uses a public Azure Blob container for its cache. Individual cached pages can be read anonymously because their source data is public. Container listing is not public, and only the App Service managed identities can write blobs.

## One-time setup

1. Create an Azure resource group.
2. Create a Microsoft Entra application or user-assigned identity for GitHub Actions and configure a federated credential for `fabian-lohauss/Analyzer` and the `production` GitHub environment.
3. Grant that identity Owner on the resource group. The infrastructure deployment creates managed-identity role assignments, which require `Microsoft.Authorization/roleAssignments/write`.
4. Add these GitHub environment variables under the `production` environment:

   | Variable | Value |
   | --- | --- |
   | `AZURE_CLIENT_ID` | Client ID of the federated identity |
   | `AZURE_TENANT_ID` | Microsoft Entra tenant ID |
   | `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |
   | `AZURE_RESOURCE_GROUP` | Existing resource group name |
   | `AZURE_APP_NAME` | Globally unique App Service name |

The identity uses OpenID Connect, so no Azure client secret or publish profile is stored in GitHub.

## Deployment

Push to `master` or run the **Deploy to Azure** workflow manually. The workflow restores, tests, publishes, provisions the resources, deploys to the staging slot, checks `/health`, and swaps staging into production.

Local development needs no Azure configuration and continues to use `App_Data/cache`. To use Blob Storage locally, set `Storage__ServiceUri` and authenticate with Azure CLI or Visual Studio credentials.