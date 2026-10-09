import Foundation
@main struct TraversalTests {
 static func main(){
  var checks=0
  func check(_ b:Bool){precondition(b);checks+=1}
  var chain=HostTraversal(root:0),found=false
  while let (value,depth)=chain.next(elapsed:0.005){if value==28{found=true};chain.append(value<28 ? [value+1]:[],at:depth)}
  check(found);check(chain.visited==29 && chain.deepest==28);check(chain.depthLimited==0 && chain.stop=="exhausted")
  var tooDeep=HostTraversal(root:0)
  while let (value,depth)=tooDeep.next(elapsed:0){tooDeep.append([value+1],at:depth)}
  check(tooDeep.visited==65 && tooDeep.depthLimited==1)
  var leaf=HostTraversal(root:0)
  while let (value,depth)=leaf.next(elapsed:0){leaf.append(value<64 ? [value+1]:[],at:depth)}
  check(leaf.deepest==64 && leaf.depthLimited==0)
  var wide=HostTraversal(root:0)
  while let (value,depth)=wide.next(elapsed:0){wide.append(value==0 ? Array(1...1000):[],at:depth)}
  check(wide.visited==800 && wide.pending==0 && wide.overflowed && !wide.complete && wide.stop=="node-limit")
  var timed=HostTraversal(root:0)
  check(timed.next(elapsed:0.2)==nil && timed.visited==0 && timed.stop=="time-limit")
  var ordering=HostTraversal(root:0),order:[Int]=[]
  while let (value,depth)=ordering.next(elapsed:0){order.append(value);ordering.append(value==0 ? [1,2]:(value==1 ? [3]:[]),at:depth)}
  check(order==[0,1,2,3])
  var generated=0,huge=HostTraversal(root:0)
  huge.append((1...1_000_000).lazy.map{value in generated+=1;return value},at:0)
  check(generated==800 && huge.pending==800 && huge.overflowed && !huge.complete)
  print("\(checks) shared traversal checks passed; synthetic trees only")
 }
}
