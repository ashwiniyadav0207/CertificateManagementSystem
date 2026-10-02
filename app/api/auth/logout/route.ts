import { NextResponse } from 'next/server';
import { getEngineUrl, getApiKey } from '@/lib/api';

export const dynamic = 'force-dynamic';

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const engineUrl = getEngineUrl();
    const apiKey = getApiKey();

    await fetch(`${engineUrl}/internal/auth/logout`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Api-Key': apiKey,
      },
      body: JSON.stringify(body),
    });

    const res = new NextResponse(null, { status: 204 });
    res.cookies.delete('credentia-session');
    return res;
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
