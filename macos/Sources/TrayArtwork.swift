import AppKit
// Shared selected option-2 mark; no second badge.
// Keep the verified 18pt artwork inside the existing 20pt slot.
enum TrayArtwork {
 static func image(dark:Bool)->NSImage {
  let image=NSImage(size:NSSize(width:20,height:20),flipped:true){_ in
   NSGraphicsContext.saveGraphicsState();defer{NSGraphicsContext.restoreGraphicsState()}
   // Restore the pre-candidate14 18pt drawing inside the unchanged 20pt slot.
   let transform=NSAffineTransform();transform.translateX(by:0,yBy:1);transform.scale(by:0.9);transform.concat()
   let rect=NSRect(x:0,y:0,width:20,height:20)
   if let url=Bundle.main.url(forResource:dark ? "quota-tray-dark":"quota-tray-light",withExtension:"ico"),let source=NSImage(contentsOf:url){source.draw(in:rect,from:.zero,operation:.sourceOver,fraction:1,respectFlipped:true,hints:nil)}
   return true
  }
  image.isTemplate=false;return image
 }
}
