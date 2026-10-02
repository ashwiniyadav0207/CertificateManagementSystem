import { NextResponse } from 'next/server';
import { getEngineUrl, getApiKey } from '@/lib/api';

export const dynamic = 'force-dynamic';

export async function GET(request: Request) {
  try {
    const engineUrl = getEngineUrl();
    const apiKey = getApiKey();
    const { searchParams } = new URL(request.url);
    const page = searchParams.get('page') || '1';
    const pageSize = searchParams.get('pageSize') || '50';
    
    const response = await fetch(`${engineUrl}/internal/certificates?page=${page}&pageSize=${pageSize}`, {
      headers: {
        'X-Api-Key': apiKey,
      },
    });
    
    if (!response.ok) {
      return NextResponse.json({ error: 'Failed to fetch certificates' }, { status: response.status });
    }
    
    const data = await response.json();
    return NextResponse.json(data);
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
