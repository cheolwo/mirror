targetScope = 'subscription'
@description('400,000 KRW에 해당하는 실제 청구 통화의 금액. 통화 확인 전 배포하지 않는다.')
@minValue(1)
param budgetAmount int
param startDate string
param endDate string
param operatorEmail string
param enabled bool = false
resource budget 'Microsoft.Consumption/budgets@2024-08-01' = {
  name: 'mirror-monthly-operations'
  properties: {
    category: 'Cost'
    amount: budgetAmount
    timeGrain: 'Monthly'
    timePeriod: { startDate: startDate, endDate: endDate }
    notifications: {
      Actual80: { enabled: enabled, operator: 'GreaterThanOrEqualTo', threshold: 80, thresholdType: 'Actual', contactEmails: [operatorEmail] }
      Actual90: { enabled: enabled, operator: 'GreaterThanOrEqualTo', threshold: 90, thresholdType: 'Actual', contactEmails: [operatorEmail] }
      Actual100: { enabled: enabled, operator: 'GreaterThanOrEqualTo', threshold: 100, thresholdType: 'Actual', contactEmails: [operatorEmail] }
      Forecast100: { enabled: enabled, operator: 'GreaterThanOrEqualTo', threshold: 100, thresholdType: 'Forecasted', contactEmails: [operatorEmail] }
    }
  }
}
