# Instruction

Running applications in a POD on AKS require access to identities in Azure Active Directory to access Azure resources.
The project [aad-pod-identity](https://github.com/Azure/aad-pod-identity) enables the usage of User assigned Identities that are
mapped to a AKS Pod. These identities can be used to access Azure resources e.g. Azure KeyVault in a secure way without the need to manage 
credentials. A User assigned Identity is created and assigned to your POD to acquire an access token for Azure resources.
RBAC is used to grant access to Azure resources to the User assigned Identity.

In this example project an ASP.NET Core project is created that uses Azure KeyVault to store application settings.
An User assigned Identity is used to access the KeyVault.

## Install Microsoft.Extensions.Configuration.AzureKeyVault

Install KeyVault extension Microsoft.Extensions.Configuration.AzureKeyVault to access application settings stored in KeyVault.

## Add configuration sources in [Program.cs](/Program.cs)

Use Microsoft.Azure.Services.AppAuthentication to acquire an access token for your User assigned Identity.
Configure your WebHostBuilder:

``` C#
public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .ConfigureAppConfiguration((ctx, builder) => 
                {
                    var config = builder.Build();
                    var tokenProvider = new AzureServiceTokenProvider();
                    var kvClient = new KeyVaultClient((authority, resource, scope) => tokenProvider.KeyVaultTokenCallback(authority, resource, scope));
                    builder.AddAzureKeyVault(Environment.GetEnvironmentVariable("KeyVault__BaseUrl"), kvClient, new DefaultKeyVaultSecretManager());
                });
```

## Create a resource group

``` Azure CLI
az group create -l westeurope -n <resourcegroup>
```

## Create a user assigned identity

Get ClientId and resource id of identity

```Azure CLI
az identity create -g <resourcegroup> -n AspnetCoreIdentity
```

## Assign Reader Role to ResourceGroup of your AKS Cluster to new Identity

``` Azure CLI
az role assignment create --role Reader --assignee <principalid> --scope /subscriptions/<subscriptionid>/resourcegroups/<resourcegroup>
```

## Assign Managed Identity Operator role to your cluster ServicePrincipal, use the object id

Find the ServicePrincipal used by your cluster.

``` Azure CLI
az role assignment create --role "Managed Identity Operator" --assignee <sp id> --scope <full id of the identity>
```

## Create an Azure KeyVault

``` Azure CLI
az keyvault create -n aspnetcorekv -g <resourcegroup> -l westeurope
```

## Assign permissions to user assigned identity

``` Azure Cli
az keyvault set-policy -n aspnetcorekv -g <resourcegroup> --secret-permissions get, list --key-permissions get, list --object-id <identity principal id> --spn AspnetCoreIdentity
```

## Add some secret to your Key Vault

```
az keyvault secret set --vault-name aspnetcorekv --name Settings--ValueOne --value ValueOne
az keyvault secret set --vault-name aspnetcorekv --name Settings--ValueTwo --value ValueTwo
```

## Setup aad-pod-identity in your AKS cluster

To setup aad-pod-identity follow the [installation guide](https://github.com/Azure/aad-pod-identity#get-started)

## Take a look at the example Kubernetes deployment.yaml

An Kubernetes object named AzureIdentity is created. You have to specify the ResourceId and ClientId of your User assigned Identity.

```YAML
apiVersion: "aadpodidentity.k8s.io/v1"
kind: AzureIdentity
metadata:
  name: aspnet-core-identity
spec:
  type: 0
  ResourceID: /subscriptions/<subscriptionid>/resourcegroups/<resourcegroup>/providers/Microsoft.ManagedIdentity/userAssignedIdentities/AspnetCoreIdentity
  ClientID: <clientid>
```

Next step is to create a binding. The value in spec.AzureIdentity must match the name of the above created AzureIdentity Kubernetes object. (metadata.name).

```YAML
apiVersion: "aadpodidentity.k8s.io/v1"
kind: AzureIdentityBinding
metadata:
  name: aspnetcoreidentity-azure-identity-binding
spec:
  AzureIdentity: aspnet-core-identity
  Selector: aspnetcoreidentity
```

Add a label named aadpodidbinding to your pod template in deployment description, that matches the value of the selector in the AzureIdentityBinding object.

```YAML
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  name: aspnetcoreidentitybackend
spec:
  replicas: 3
  minReadySeconds: 5
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 1
  template:
    metadata:
      labels:
        name: aspnetcoreidentitybackend
        app: aspnetcoreidentity
        aadpodidbinding: aspnetcoreidentity
```