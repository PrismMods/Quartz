#nullable enable
namespace Quartz.Features.Minecraft;
internal static class McPointerLock {
    // Offscreen CEF cannot grant pointer lock — requestPointerLock throws "The root
    // document of this element is not valid for pointer lock" — so Minecraft Classic
    // falls back to drag-look, which is why looking around needed a held button and
    // died the moment the cursor left the view. This shim makes the page believe the
    // lock succeeded and synthesises movementX/movementY from successive positions,
    // so plain mouse movement drives the camera. Quartz then locks the OS cursor and
    // wraps its virtual pointer back to the centre; the jump exceeds WRAP and is
    // dropped, so wrapping never shows up as a camera snap.
    public const int WrapThreshold = 150;
    public const string Script = """
(function(){
 if(window.__quartzPointerLock)return;
 window.__quartzPointerLock=1;
 var locked=false,lx=null,ly=null,dx=0,dy=0,WRAP=150;
 var el=function(){return document.querySelector('canvas')||document.body;};
 Object.defineProperty(document,'pointerLockElement',{configurable:true,get:function(){return locked?el():null;}});
 Element.prototype.requestPointerLock=function(){locked=true;lx=null;ly=null;
  document.dispatchEvent(new Event('pointerlockchange'));return Promise.resolve();};
 document.exitPointerLock=function(){locked=false;
  document.dispatchEvent(new Event('pointerlockchange'));};
 window.addEventListener('mousemove',function(e){
  if(lx===null){dx=0;dy=0;}
  else{dx=e.clientX-lx;dy=e.clientY-ly;
   if(Math.abs(dx)>WRAP||Math.abs(dy)>WRAP){dx=0;dy=0;}}
  lx=e.clientX;ly=e.clientY;},true);
 Object.defineProperty(MouseEvent.prototype,'movementX',{configurable:true,get:function(){return dx;}});
 Object.defineProperty(MouseEvent.prototype,'movementY',{configurable:true,get:function(){return dy;}});
})();
""";
}
