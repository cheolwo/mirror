targetScope = 'resourceGroup'
param location string = 'koreacentral'
param prefix string = 'mirror-ops'
param operatorEmail string
param publicBaseUrl string
param vmResourceId string
param enabled bool = false

resource group 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: '${prefix}-email'
  location: 'global'
  properties: {
    groupShortName: 'MirrorOps'
    enabled: enabled
    emailReceivers: [{ name: 'Operator', emailAddress: operatorEmail, useCommonAlertSchema: true }]
  }
}
resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${prefix}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
    workspaceCapping: { dailyQuotaGb: json('0.1') }
  }
}
resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${prefix}-availability'
  location: location
  kind: 'web'
  properties: { Application_Type: 'web', WorkspaceResourceId: workspace.id, IngestionMode: 'LogAnalytics' }
}
resource tests 'Microsoft.Insights/webtests@2022-06-15' = [for endpoint in ['live', 'ready']: {
  name: '${prefix}-${endpoint}'
  location: location
  kind: 'standard'
  tags: { 'hidden-link:${insights.id}': 'Resource' }
  properties: {
    SyntheticMonitorId: '${prefix}-${endpoint}'
    Name: '${prefix}-${endpoint}'
    Enabled: enabled
    Frequency: 300
    Timeout: 30
    Kind: 'standard'
    RetryEnabled: true
    Locations: [{ Id: 'apac-sg-sin-azr' }, { Id: 'apac-jp-kaw-edge' }, { Id: 'apac-hk-hkn-azr' }]
    Request: { RequestUrl: '${publicBaseUrl}/health/${endpoint}', HttpVerb: 'GET' }
    ValidationRules: { ExpectedHttpStatusCode: 200, SSLCheck: true, SSLCertRemainingLifetimeCheck: 7 }
  }
}]
resource failures 'Microsoft.Insights/metricAlerts@2018-03-01' = [for (endpoint, i) in ['live', 'ready']: {
  name: '${prefix}-${endpoint}-failed'
  location: 'global'
  properties: {
    enabled: enabled
    severity: 1
    scopes: [tests[i].id, insights.id]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: { 'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria', webTestId: tests[i].id, componentId: insights.id, failedLocationCount: 2 }
    actions: [{ actionGroupId: group.id }]
  }
}]
resource cpu 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${prefix}-cpu'
  location: 'global'
  properties: {
    enabled: enabled
    severity: 2
    scopes: [vmResourceId]
    targetResourceType: 'Microsoft.Compute/virtualMachines'
    targetResourceRegion: location
    evaluationFrequency: 'PT1M'
    windowSize: 'PT15M'
    criteria: { 'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria', allOf: [{ criterionType: 'StaticThresholdCriterion', name: 'CpuHigh', metricName: 'Percentage CPU', metricNamespace: 'Microsoft.Compute/virtualMachines', operator: 'GreaterThan', threshold: 85, timeAggregation: 'Average' }] }
    actions: [{ actionGroupId: group.id }]
  }
}
// AMA 설정 이후 활성화한다. 빈 관찰도 문제로 다뤄 백업/수집 실패를 침묵시키지 않는다.
resource logAlerts 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = [for item in [
  { code: 'disk', window: 'PT15M', query: 'Perf | where TimeGenerated > ago(15m) | where ObjectName == "Logical Disk" and CounterName == "% Free Space" | summarize Free=min(CounterValue) | where isnull(Free) or Free < 15' }
  { code: 'backup', window: 'P2D', query: 'Syslog | where TimeGenerated > ago(26h) | where ProcessName == "mirror-backup" and SyslogMessage == "BACKUP_OK" | summarize Count=count() | where Count == 0' }
  { code: 'heartbeat', window: 'PT15M', query: 'Heartbeat | where TimeGenerated > ago(10m) | summarize Count=count() | where Count == 0' }
]: {
  name: '${prefix}-${item.code}'
  location: location
  properties: {
    enabled: enabled
    severity: 1
    scopes: [workspace.id]
    evaluationFrequency: 'PT5M'
    windowSize: item.window
    criteria: { allOf: [{ query: item.query, timeAggregation: 'Count', operator: 'GreaterThan', threshold: 0, failingPeriods: { numberOfEvaluationPeriods: 1, minFailingPeriodsToAlert: 1 } }] }
    actions: { actionGroups: [group.id] }
  }
}]
output actionGroupId string = group.id
output workspaceId string = workspace.id

// 같은 resource group의 기존 VM. 새 VM/유료 SKU는 자동 생성하지 않는다.
resource vm 'Microsoft.Compute/virtualMachines@2024-07-01' existing = { name: last(split(vmResourceId, '/')) }
resource agent 'Microsoft.Compute/virtualMachines/extensions@2024-07-01' = {
  parent: vm
  name: 'AzureMonitorLinuxAgent'
  location: location
  properties: { publisher: 'Microsoft.Azure.Monitor', type: 'AzureMonitorLinuxAgent', typeHandlerVersion: '1.0', autoUpgradeMinorVersion: true, enableAutomaticUpgrade: true }
}
resource collection 'Microsoft.Insights/dataCollectionRules@2023-03-11' = {
  name: '${prefix}-vm-data'
  location: location
  kind: 'Linux'
  properties: {
    destinations: { logAnalytics: [{ name: 'operations', workspaceResourceId: workspace.id }] }
    dataSources: {
      performanceCounters: [{ name: 'disk', streams: ['Microsoft-Perf'], samplingFrequencyInSeconds: 60, counterSpecifiers: ['\\Logical Disk(*)\\% Free Space'] }]
      syslog: [{ name: 'backup', streams: ['Microsoft-Syslog'], facilityNames: ['user'], logLevels: ['Info'] }]
    }
    dataFlows: [
      { streams: ['Microsoft-Perf'], destinations: ['operations'] }
      { streams: ['Microsoft-Syslog'], destinations: ['operations'], transformKql: 'source | where ProcessName == "mirror-backup" | where SyslogMessage == "BACKUP_OK"', outputStream: 'Microsoft-Syslog' }
    ]
  }
}
resource association 'Microsoft.Insights/dataCollectionRuleAssociations@2023-03-11' = {
  name: '${prefix}-vm-data'
  scope: vm
  properties: { dataCollectionRuleId: collection.id }
  dependsOn: [agent]
}
