1. 아래의 상품상세 URL에서 아래 스크립트를 날리면 UserId_Peed_Product.csv에 넣어야하는 값을 알 수 있다.


https://www.glowpick.com/beauty/ranking?id=36


var itemList = document.querySelectorAll("li[itemprop='itemListElement'] meta[itemprop='url'")

itemList.forEach(function(item){
       console.log( item.content.substring( item.content.lastIndexOf("/") + 1,  item.content.length - 1));
});


2. 피드가 부족할 경우 1과 같이 사용한다.