import { NextResponse } from 'next/server';
import { getEngineUrl, getApiKey } from '@/lib/api';

export const dynamic = 'force-dynamic';

export async function POST(
  request: Request,
  { params }: { params: Promise<{ publicId: string }> }
) {
  try {
    const { publicId } = await params;
    const { reason } = await request.json();
    const engineUrl = getEngineUrl();
    const apiKey = getApiKey();
    
    const response = await fetch(`${engineUrl}/internal/certificates/${publicId}/revoke`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Api-Key': apiKey,
      },
      body: JSON.stringify({ reason }),
    });
    
    if (!response.ok) {
      return NextResponse.json({ error: 'Revocation failed' }, { status: response.status });
    }
    
    return NextResponse.json({ success: true });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
