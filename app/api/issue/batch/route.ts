import { NextResponse } from 'next/server';
import { getEngineUrl, getApiKey } from '@/lib/api';

export const dynamic = 'force-dynamic';

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const { recipients, eventName } = body;
    const engineUrl = getEngineUrl();
    const apiKey = getApiKey();
    
    if (!Array.isArray(recipients)) {
      return NextResponse.json({ error: 'Recipients must be an array' }, { status: 400 });
    }
    
    const results = [];
    for (const recipient of recipients) {
      try {
        const response = await fetch(`${engineUrl}/internal/demo/issue`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'X-Api-Key': apiKey,
          },
          body: JSON.stringify({ fullName: recipient.name, eventName }),
        });
        
        if (response.ok) {
          const data = await response.json();
          results.push({ success: true, name: recipient.name, data });
        } else {
          results.push({ success: false, name: recipient.name, status: response.status });
        }
      } catch (err: any) {
        results.push({ success: false, name: recipient.name, error: err.message });
      }
    }
    
    return NextResponse.json(results);
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
