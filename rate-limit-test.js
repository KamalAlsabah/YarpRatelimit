(async function rateLimitTester() {
  const BASE_URL = 'http://localhost:5000';
  
  const scenarios = [
    {
      name: 'Whitelist: Localhost Health Check',
      requests: 50,
      parallel: false,
      headers: {},
      path: '/health'
    },
    {
      name: 'Whitelist: Premium Tenant 88',
      requests: 50,
      parallel: false,
      headers: { 'Abp-TenantId': '88' },
      path: '/api/service1/test'
    },
    {
      name: 'Whitelist: Internal CRM Client from 192.168.1.0/24',
      requests: 50,
      parallel: false,
      headers: { 'X-Forwarded-For': '192.168.1.100', 'X-Client-Id': 'crm' },
      path: '/api/service1/test'
    },
    {
      name: 'Route Rule: Service1 API (TokenBucket 150 limit)',
      requests: 50,
      parallel: false,
      headers: {},
      path: '/api/service1/test'
    },
    {
      name: 'Route Rule: Service2 API (FixedWindow 50 limit)',
      requests: 50,
      parallel: false,
      headers: {},
      path: '/api/service2/test'
    },
    {
      name: 'Tenant Rule: Premium Tenant 1 (Concurrency 30)',
      requests: 20,
      parallel: true,
      headers: { 'Abp-TenantId': '1' },
      path: '/api/service1/test'
    },
    {
      name: 'Tenant Rule: Premium Tenant 4 CRM (Concurrency 100)',
      requests: 50,
      parallel: true,
      headers: { 'Abp-TenantId': '4', 'X-Client-Id': 'crm' },
      path: '/api/service1/test'
    },
    {
      name: 'Tenant Rule: Standard Tenant 5 (FixedWindow 100)',
      requests: 60,
      parallel: false,
      headers: { 'Abp-TenantId': '5', 'X-Client-Id': 'crm' },
      path: '/api/service1/test'
    },
    {
      name: 'Tenant Rule: Enterprise Tenant 6 (SlidingWindow 500)',
      requests: 100,
      parallel: false,
      headers: { 'Abp-TenantId': '6', 'X-Client-Id': 'web-app' },
      path: '/api/service1/test'
    },
    {
      name: 'Tenant Rule: Basic Tenant 10 (TokenBucket 50)',
      requests: 30,
      parallel: false,
      headers: { 'Abp-TenantId': '10' },
      path: '/api/service1/test'
    },
    {
      name: 'Anonymous: Global Default (TokenBucket 5)',
      requests: 20,
      parallel: false,
      headers: { 'X-Forwarded-For': '203.0.113.45' },
      path: '/api/service1/test'
    },
    {
      name: 'Anonymous: Different IPs Sequential',
      requests: 30,
      parallel: false,
      headers: { 'X-Forwarded-For': '198.51.100.100' },
      path: '/api/service1/test'
    },
    {
      name: 'Concurrent Requests: Different Tenants',
      requests: 15,
      parallel: true,
      customizer: (index) => ({
        headers: { 'Abp-TenantId': String((index % 4) + 1) }
      }),
      path: '/api/service1/test'
    },
    {
      name: 'Mixed Routes: Service1 vs Service2',
      requests: 40,
      parallel: false,
      customizer: (index) => ({
        path: index % 2 === 0 ? '/api/service1/test' : '/api/service2/test',
        headers: { 'Abp-TenantId': '1' }
      })
    }
  ];

  const results = [];

  async function makeRequest(url, headers = {}) {
    const defaultHeaders = {
      'Content-Type': 'application/json'
    };
    const mergedHeaders = { ...defaultHeaders, ...headers };

    try {
      const response = await fetch(url, {
        method: 'GET',
        headers: mergedHeaders
      });

      return {
        status: response.status,
        limit: response.headers.get('X-RateLimit-Limit'),
        remaining: response.headers.get('X-RateLimit-Remaining'),
        reset: response.headers.get('X-RateLimit-Reset'),
        retryAfter: response.headers.get('Retry-After')
      };
    } catch (error) {
      return {
        status: 0,
        error: error.message
      };
    }
  }

  async function executeScenario(scenario) {
    const startTime = performance.now();
    const path = scenario.path || '/api/service1/test';
    const url = `${BASE_URL}${path}`;

    const requests = [];

    if (scenario.parallel) {
      for (let i = 0; i < scenario.requests; i++) {
        const customHeaders = scenario.customizer ? scenario.customizer(i).headers : scenario.headers;
        requests.push(makeRequest(url, customHeaders));
      }
      const responses = await Promise.all(requests);

      const endTime = performance.now();
      return {
        scenario: scenario.name,
        totalHits: responses.length,
        status200: responses.filter(r => r.status === 200).length,
        status429: responses.filter(r => r.status === 429).length,
        statusOther: responses.filter(r => r.status !== 200 && r.status !== 429 && r.status !== 0).length,
        statusError: responses.filter(r => r.status === 0).length,
        finalRemaining: responses[responses.length - 1]?.remaining || 'N/A',
        finalRetryAfter: responses[responses.length - 1]?.retryAfter || 'N/A',
        finalLimit: responses[responses.length - 1]?.limit || 'N/A',
        duration: (endTime - startTime).toFixed(2),
        executionMode: 'Parallel'
      };
    } else {
      for (let i = 0; i < scenario.requests; i++) {
        const customHeaders = scenario.customizer ? scenario.customizer(i).headers : scenario.headers;
        const response = await makeRequest(url, customHeaders);
        requests.push(response);
      }

      const endTime = performance.now();
      return {
        scenario: scenario.name,
        totalHits: requests.length,
        status200: requests.filter(r => r.status === 200).length,
        status429: requests.filter(r => r.status === 429).length,
        statusOther: requests.filter(r => r.status !== 200 && r.status !== 429 && r.status !== 0).length,
        statusError: requests.filter(r => r.status === 0).length,
        finalRemaining: requests[requests.length - 1]?.remaining || 'N/A',
        finalRetryAfter: requests[requests.length - 1]?.retryAfter || 'N/A',
        finalLimit: requests[requests.length - 1]?.limit || 'N/A',
        duration: (endTime - startTime).toFixed(2),
        executionMode: 'Sequential'
      };
    }
  }

  console.log('🚀 Starting Rate Limit Test Suite...\n');
  console.log(`📍 Target: ${BASE_URL}`);
  console.log(`📊 Scenarios: ${scenarios.length}\n`);

  for (const scenario of scenarios) {
    console.log(`⏳ Running: ${scenario.name}...`);
    const result = await executeScenario(scenario);
    results.push(result);
  }

  console.log('\n✅ All scenarios completed!\n');
  console.log('📋 FINAL REPORT:');
  console.table(results);

  const summary = {
    totalScenarios: scenarios.length,
    totalRequests: results.reduce((sum, r) => sum + r.totalHits, 0),
    total200: results.reduce((sum, r) => sum + r.status200, 0),
    total429: results.reduce((sum, r) => sum + r.status429, 0),
    totalOther: results.reduce((sum, r) => sum + r.statusOther, 0),
    totalErrors: results.reduce((sum, r) => sum + r.statusError, 0)
  };

  console.log('\n📊 SUMMARY:');
  console.table([summary]);

  return results;
})();
