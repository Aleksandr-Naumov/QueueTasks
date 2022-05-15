# QueueTasks
Библиотека(Фреймворк) для получения "задач"(или чего угодно) из очереди пользователем с применением SSE (API)


Применение: </br>
1)Entry. </br>
2)ITaskManager.Add </br>
3)Написать реализацию IExtensionService, IProvider </br>
...

не отображаются на фронте сообщений => добавить "compress: false," - Исправлен баг работы SSE в webpack.config 


const eventSource = new EventSource(resoureUrl, {
            headers: {
                'Authorization': 'Bearer ' + authorizationToken
            }
        }); </br>
let eventSource = new EventSource("/api/v1/QueueTasks/wait-sse"); </br>
eventSource.onmessage = function(event) {
  console.log("Новое сообщение", event.data);
  // этот код выведет в консоль 3 сообщения, из потока данных выше
};

eventSource.addEventListener('connection', event => {
  alert(${event.data} подключилось);
}); </br>
eventSource.addEventListener('task', event => {
  alert(Пришла таска: ${event.data});
});

Ссылки: </br>
Nginx - https://stackoverflow.com/questions/13672743/eventsource-server-sent-events-through-nginx </br>
PHP - https://developer.mozilla.org/ru/docs/Web/API/Server-sent_events/Using_server-sent_events </br>
JavaScript - https://learn.javascript.ru/server-sent-events </br>
