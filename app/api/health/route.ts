import { NextResponse } from 'next/server';
import { getEngineUrl, getApiKey } from '@/lib/api';

export const dynamic = 'force-dynamic';

export async function GET() {
  try {
    const engineUrl = getEngineUrl();
    const apiKey = getApiKey();
    
    const response = await fetch(`${engineUrl}/internal/health`, {
      headers: {
        'X-Api-Key': apiKey,
      },
    });
    
    if (!response.ok) {
      return NextResponse.json({ error: 'Health check failed' }, { status: response.status });
    }
    
    const data = await response.text();
    let jsonData = {};
    try {
      jsonData = data ? JSON.parse(data) : {};
    } catch (e) {
      // response might not be json
      jsonData = { status: data };
    }
    return NextResponse.json(jsonData);
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
