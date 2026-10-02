import { NextResponse } from 'next/server';
import fs from 'node:fs';
import { getCertificatePdfPath } from '@/lib/server/certificates';

export const dynamic = 'force-dynamic';

export async function GET(
  request: Request,
  { params }: { params: Promise<{ publicId: string }> }
) {
  try {
    const { publicId } = await params;
    const pdfPath = getCertificatePdfPath(publicId);
    
    if (!pdfPath || !fs.existsSync(pdfPath)) {
      return NextResponse.json({ error: 'Certificate PDF artifact not found' }, { status: 404 });
    }

    const fileBuffer = fs.readFileSync(pdfPath);
    return new NextResponse(fileBuffer, {
      status: 200,
      headers: {
        'Content-Type': 'application/pdf',
        'Content-Disposition': `inline; filename="certificate-${publicId}.pdf"`,
        'Content-Length': fileBuffer.length.toString(),
      },
    });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
