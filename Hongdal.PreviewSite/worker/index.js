function withSecurityHeaders(response) {
  const headers = new Headers(response.headers);
  headers.set("X-Content-Type-Options", "nosniff");
  headers.set("Referrer-Policy", "strict-origin-when-cross-origin");
  headers.set("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers
  });
}

export default {
  async fetch(request, env) {
    if (request.method !== "GET" && request.method !== "HEAD") {
      return new Response("Method Not Allowed", {
        status: 405,
        headers: { Allow: "GET, HEAD" }
      });
    }

    const assetResponse = await env.ASSETS.fetch(request);
    if (assetResponse.status !== 404) {
      return withSecurityHeaders(assetResponse);
    }

    const accept = request.headers.get("Accept") || "";
    if (!accept.includes("text/html")) {
      return withSecurityHeaders(assetResponse);
    }

    const fallbackUrl = new URL("/", request.url);
    const fallbackRequest = new Request(fallbackUrl, request);
    const fallbackResponse = await env.ASSETS.fetch(fallbackRequest);
    return withSecurityHeaders(fallbackResponse);
  }
};
