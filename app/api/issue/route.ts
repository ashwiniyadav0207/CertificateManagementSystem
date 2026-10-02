import { NextResponse } from 'next/server';
import { getEngineUrl, getApiKey } from '@/lib/api';

export const dynamic = 'force-dynamic';

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const { fullName, eventName } = body;
    const engineUrl = getEngineUrl();
    const apiKey = getApiKey();
    
    const response = await fetch(`${engineUrl}/internal/demo/issue`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Api-Key': apiKey,
      },
      body: JSON.stringify({ fullName, eventName }),
    });
    
    if (!response.ok) {
      return NextResponse.json({ error: 'Issuing failed' }, { status: response.status });
    }
    
    const data = await response.json();
    return NextResponse.json(data);
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
