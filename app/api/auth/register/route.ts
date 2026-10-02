import { NextResponse } from 'next/server';
import { getEngineUrl, getApiKey } from '@/lib/api';

export const dynamic = 'force-dynamic';

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const engineUrl = getEngineUrl();
    const apiKey = getApiKey();

    const response = await fetch(`${engineUrl}/internal/auth/register`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Api-Key': apiKey,
      },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      const status = response.status;
      const msg = status === 409 ? 'An account with this email already exists' : 'Registration failed';
      return NextResponse.json({ error: msg }, { status });
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
