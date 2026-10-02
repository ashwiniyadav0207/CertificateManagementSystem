import { NextResponse } from 'next/server';
import { getVerificationUrl } from '@/lib/api';

export const dynamic = 'force-dynamic';

export async function GET(
  request: Request,
  { params }: { params: Promise<{ publicId: string }> }
) {
  try {
    const { publicId } = await params;
    const verificationUrl = getVerificationUrl();
    
    const response = await fetch(`${verificationUrl}/api/verify/${publicId}`);
    
    if (!response.ok) {
      return NextResponse.json({ error: 'Verification failed' }, { status: response.status });
    }
    
    const data = await response.json();
    return NextResponse.json(data);
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
