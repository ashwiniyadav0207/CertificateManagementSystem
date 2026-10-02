import { NextResponse } from 'next/server';
import { getEngineUrl, getApiKey } from '@/lib/api';

export const dynamic = 'force-dynamic';

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const engineUrl = getEngineUrl();
    const apiKey = getApiKey();

    const response = await fetch(`${engineUrl}/internal/auth/login`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Api-Key': apiKey,
      },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      return NextResponse.json(
        { error: 'Invalid email or password' },
        { status: response.status }
      );
    }

    const data = await response.json();
    const res = NextResponse.json(data);
    res.cookies.set('credentia-session', '1', {
      path: '/',
      maxAge: 60 * 60 * 24 * 30, // 30 days
      httpOnly: false,
      sameSite: 'lax',
    });
    return res;
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
