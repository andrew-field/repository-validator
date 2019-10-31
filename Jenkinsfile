library 'jenkins-ptcs-library@2.1.0'

def isMaster(branchName) {return branchName == "master"}
def isTest(branchName) {return branchName == "test"}

podTemplate(label: pod.label,
  containers: pod.templates + [
    containerTemplate(name: 'dotnet', image: 'microsoft/dotnet:2.2-sdk', ttyEnabled: true, command: '/bin/sh -c', args: 'cat'),
    containerTemplate(name: 'powershell', image: 'azuresdk/azure-powershell-core:master', ttyEnabled: true, command: '/bin/sh -c', args: 'cat')
  ]
) {

    def branch = (env.BRANCH_NAME)
    def buildNumber = (env.BUILD_NUMBER)
    def resourceGroup = 'repository-validator-prod'
    def appName = 'ptcs-github-validator'
    def gitHubOrganization = 'protacon'

    def functionsProject = 'ValidationLibrary.AzureFunctions'
    def zipName = 'publish.zip'
    def publishFolder = 'publish'

    node(pod.label) {
        stage('Checkout') {
            checkout scm
        }
        container('dotnet') {
            stage('Build') {
                sh """
                    dotnet publish -c Release -o $publishFolder $functionsProject --version-suffix ${env.BUILD_NUMBER}
                """
            }
            stage('Test') {
                sh """
                    dotnet test
                """
            }
        }
        if (isTest(branch) || isMaster(branch)){
            container('powershell') {
                stage('Package') {
                    sh """
                        pwsh -command "&./Deployment/Zip.ps1 -Destination $zipName -PublishFolder $functionsProject/$publishFolder"
                    """
                }

                if (isTest(branch)){
                    withCredentials([azureServicePrincipal('HJNI-MSDN-Subscriptions')]) {
                        def ciRg = 'repo-ci-' + buildNumber
                        def ciAppName = 'repo-ci-' + buildNumber

                        stage('Login to test'){
                            sh """
                                pwsh -command "./Deployment/Login.ps1 -ApplicationId '$AZURE_CLIENT_ID' -ApplicationKey '$AZURE_CLIENT_SECRET' -TenantId '$AZURE_TENANT_ID'"
                            """
                        }
                        stage('Create temporary Resource Group'){
                            sh """
                                pwsh -command "New-AzResourceGroup -Name '$ciRg' -Location 'North Europe'"
                            """
                        }
                        stage('Create test environment'){
                            sh """
                                pwsh -command "New-AzResourceGroupDeployment -Name github-validator -TemplateFile Deployment/azuredeploy.json -ResourceGroupName $ciRg -appName $ciAppName -gitHubToken (ConvertTo-SecureString -String 'MOCKTOKEN' -AsPlainText -Force) -gitHubOrganization $gitHubOrganization -environment Development"
                            """
                        }
                        catchError(buildResult: 'FAILURE', stageResult: 'FAILURE') {
                            stage('Publish to test environment') {
                                sh """
                                    pwsh -command "&./Deployment/Deploy.ps1 -ResourceGroup $ciRg -WebAppName $ciAppName -ZipFilePath $zipName"
                                """
                            }
                            stage('Create .runsettings-file acceptance tests') {
                                sh """
                                    pwsh -command "&./Deployment/Create-RunSettingsFile.ps1 -ResourceGroup $ciRg -WebAppName $ciAppName"
                                """
                            }
                            container('dotnet') {
                                stage('Acceptance tests') {
                                    sh """
                                        cd AcceptanceTests
                                        dotnet test --settings .runsettings
                                        cd ..
                                    """
                                }
                            }
                        }
                        stage('Delete test environment'){
                            sh """
                                pwsh -command "Remove-AzResourceGroup -Name '$ciRg' -Force"
                            """
                        }
                    }
                }
                if (isMaster(branch)){
                    withCredentials([
                        string(credentialsId: 'hjni_azure_sp_id', variable: 'SP_APPLICATION'),
                        string(credentialsId: 'hjni_azure_sp_key', variable: 'SP_KEY'),
                        string(credentialsId: 'hjni_azure_sp_tenant', variable: 'SP_TENANT'),
                        ]){
                        stage('Login to production'){
                            sh """
                                pwsh -command "./Deployment/Login.ps1 -ApplicationId '$SP_APPLICATION' -ApplicationKey '$SP_KEY' -TenantId '$SP_TENANT'"
                            """
                        }
                    }
                    withCredentials([
                        string(credentialsId: 'hjni_github_token', variable: 'GH_TOKEN')
                    ]){
                        stage('Create production environment') {
                            sh """
                                pwsh -command "New-AzResourceGroupDeployment -Name github-validator -TemplateFile Deployment/azuredeploy.json -ResourceGroupName $resourceGroup -appName $appName -gitHubToken (ConvertTo-SecureString -String $GH_TOKEN -AsPlainText -Force) -gitHubOrganization $gitHubOrganization -environment Development"
                            """
                        }
                    }
                    stage('Publish to production environment') {
                        sh """
                            pwsh -command "&./Deployment/Deploy.ps1 -ResourceGroup $resourceGroup -WebAppName $appName -ZipFilePath $zipName"
                        """
                    }
                }
            }
        }
    }
  }
