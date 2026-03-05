export async function apiPost(url, body) {
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });

  if (!res.ok) {
    throw new Error(await parseError(res));
  }

  return res.json();
}

export async function apiGet(url) {
  const res = await fetch(url);
  if (!res.ok) {
    throw new Error(await parseError(res));
  }

  return res.json();
}

async function parseError(res) {
  try {
    const data = await res.json();

    if (data?.error) {
      return data.error;
    }

    if (data?.detail) {
      return data.detail;
    }

    if (data?.title) {
      return data.title;
    }

    if (data?.errors && typeof data.errors === "object") {
      const firstKey = Object.keys(data.errors)[0];
      if (firstKey && Array.isArray(data.errors[firstKey]) && data.errors[firstKey].length) {
        return data.errors[firstKey][0];
      }
    }

    return `Request failed (${res.status})`;
  } catch {
    return `Request failed (${res.status})`;
  }
}
