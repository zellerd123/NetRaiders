from multiprocessing import cpu_count

bind = 'unix:/tmp/gunicorn.sock'

workers = cpu_count() +1
worker_class = 'uvicorn.workers.UvicornWorker'

loglevel = 'error'
accessslog = '/users/dorlando/netraiders/bin/access_log'
errorlog = '/users/dorlando/netraiders/bin/error_log'
