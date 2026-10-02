import { NextResponse } from 'next/server';
import { getEngineUrl, getApiKey } from '@/lib/api';

export const dynamic = 'force-dynamic';

export async function GET() {
  try {
    const engineUrl = getEngineUrl();
    const apiKey = getApiKey();
    
    const response = await fetch(`${engineUrl}/internal/events`, {
      headers: {
        'X-Api-Key': apiKey,
      },
    });
    
    if (!response.ok) {
      return NextResponse.json({ error: 'Failed to fetch events' }, { status: response.status });
    }
    
    const data = await response.json();
    return NextResponse.json(data);
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
