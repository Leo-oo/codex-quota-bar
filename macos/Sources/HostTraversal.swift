// Shared by the production adapter and synthetic-tree regression tests.
// Deep web accessibility wrappers must not exhaust a shallow level cap.
struct HostTraversal<Node> {
 static var depthLimit:Int {64}
 static var nodeLimit:Int {800}
 private var queue:[(Node,Int)]
 private var head=0
 private(set) var depthLimited=0
 private(set) var deepest=0
 private(set) var overflowed=false
 var complete:Bool {pending==0 && depthLimited==0 && !overflowed}
 var visited:Int {head}
 var pending:Int {queue.count-head}
 init(root:Node){queue=[(root,0)]}
 mutating func next(elapsed:Double)->(Node,Int)? {
  guard pending>0,head<Self.nodeLimit,elapsed<0.2 else{return nil}
  let value=queue[head];head+=1;deepest=max(deepest,value.1);return value
 }
 mutating func append<S:Sequence>(_ children:S,at depth:Int) where S.Element==Node {
  var iterator=children.makeIterator()
  guard let first=iterator.next() else{return}
  guard depth<Self.depthLimit else{depthLimited+=1;return}
  var item:Node?=first
  while let value=item {
   guard queue.count<Self.nodeLimit else{overflowed=true;return}
   queue.append((value,depth+1));item=iterator.next()
  }
 }
 var stop:String {overflowed ? "node-limit":pending==0 ? "exhausted":(visited>=Self.nodeLimit ? "node-limit":"time-limit")}
}
