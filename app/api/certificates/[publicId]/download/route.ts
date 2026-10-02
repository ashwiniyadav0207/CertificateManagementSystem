import { NextResponse } from 'next/server';
import { getEngineUrl, getApiKey } from '@/lib/api';

export const dynamic = 'force-dynamic';

export async function GET(
  request: Request,
  { params }: { params: Promise<{ publicId: string }> }
) {
  try {
    const { publicId } = await params;
    const engineUrl = getEngineUrl();
    const apiKey = getApiKey();
    
    const response = await fetch(`${engineUrl}/manage/certificates/${publicId}/download`, {
      headers: {
        'X-Api-Key': apiKey,
      },
    });
    
    if (!response.ok) {
      return NextResponse.json({ error: 'Download failed' }, { status: response.status });
    }
    
    // Stream the PDF response back
    const blob = await response.blob();
    const headers = new Headers(response.headers);
    
    return new NextResponse(blob, {
      status: response.status,
      headers,
    });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
