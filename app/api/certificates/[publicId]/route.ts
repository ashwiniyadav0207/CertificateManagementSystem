import { NextResponse } from 'next/server';
import { getCertificateByPublicId } from '@/lib/server/certificates';

export const dynamic = 'force-dynamic';

export async function GET(
  request: Request,
  { params }: { params: Promise<{ publicId: string }> }
) {
  try {
    const { publicId } = await params;
    const cert = getCertificateByPublicId(publicId);
    if (!cert) {
      return NextResponse.json({ error: 'Certificate not found' }, { status: 404 });
    }
    return NextResponse.json(cert);
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
