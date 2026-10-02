import { NextResponse } from 'next/server';
import { revokeCertificate } from '@/lib/server/certificates';

export const dynamic = 'force-dynamic';

export async function POST(
  request: Request,
  { params }: { params: Promise<{ publicId: string }> }
) {
  try {
    const { publicId } = await params;
    const body = await request.json().catch(() => ({}));
    const reason = body?.reason || "Revoked by administrative authority";

    const success = revokeCertificate(publicId, reason);
    if (!success) {
      return NextResponse.json({ error: 'Certificate not found or already revoked' }, { status: 404 });
    }

    return NextResponse.json({ success: true, status: 'Revoked', revocationReason: reason });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
